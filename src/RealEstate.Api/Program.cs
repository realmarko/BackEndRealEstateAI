using System.Text;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RealEstate.Api.Data;
using RealEstate.Api.Middleware;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Error tracking (Sentry) ----
// Dsn left empty (CHANGE_ME below) until a real Sentry project exists — the SDK no-ops silently
// with an empty Dsn instead of throwing, so this is safe to leave unconfigured. ErrorLogService
// (below) covers the in-app "what broke, for whom" view either way; Sentry adds full stack
// traces, breadcrumbs, and alerting on top once a real Dsn is set.
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
    options.Environment = builder.Environment.EnvironmentName;
    options.TracesSampleRate = 0.1;
});

// ---- Configuration ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// ---- AWS S3 (property image storage) ----
builder.Services.Configure<S3Options>(builder.Configuration.GetSection("AWS:S3"));
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddScoped<IS3UploadService, S3UploadService>();
builder.Services.AddScoped<IPhotoUploadService, PhotoUploadService>();

// ---- Email (SMTP) ----
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// ---- Frontend (for building links back into the app from server-sent emails) ----
builder.Services.Configure<FrontendOptions>(builder.Configuration.GetSection("Frontend"));

// ---- INEGI DENUE (business-density lookups for the map's opportunity-analysis tools) ----
builder.Services.Configure<DenueOptions>(builder.Configuration.GetSection("Inegi:Denue"));
// 5s, not the default 100s or the 10s first tried: the map fires this on every click with no
// debounce, and DENUE is best-effort (Places is the guaranteed fallback), so a slow/unreachable
// DENUE should not make every click feel stuck for long.
builder.Services.AddHttpClient<IDenueService, DenueService>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<IPopulationDensityService, PopulationDensityService>();

// ---- Error logging (in-app admin view, alongside Sentry above) ----
builder.Services.AddScoped<IErrorLogService, ErrorLogService>();

// ---- Database (PostgreSQL) ----
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// The NTS plugin must be registered on the NpgsqlDataSource itself — passing UseNetTopologySuite
// as a UseNpgsql(...) callback silently fails to wire it into Npgsql 8's type-info resolver
// pipeline, so a NetTopologySuite.Geometries.Point parameter throws InvalidCastException instead
// of mapping to the "geometry" column type PostGIS expects.
// Registered as a singleton (not just a local variable) so the container disposes its connection
// pool on shutdown, and shared by both contexts so the app opens one pool against Postgres
// instead of two — RealEstateDbContext has no geometry columns, but NTS support is additive and
// doesn't affect its plain entities.
var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
npgsqlDataSourceBuilder.UseNetTopologySuite();
builder.Services.AddSingleton(npgsqlDataSourceBuilder.Build());
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.UseNetTopologySuite()).UseSnakeCaseNamingConvention());
builder.Services.AddDbContext<RealEstateDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()).UseSnakeCaseNamingConvention());

// ---- Identity ----
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---- JWT Auth ----
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<ITokenService, TokenService>();

// ---- Rate limiting (anonymous public endpoints that trigger an external side effect) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("contact", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    // Per-IP: an anonymous write endpoint (POST /api/errors) is exactly the shape that gets
    // abused to flood a database/Sentry quota — a global window would let one bad actor block
    // every real visitor's own error reports too.
    options.AddPolicy("errors", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    // Per-IP, not AddFixedWindowLimiter's single shared window: the map fires this on every
    // click with no debounce, so a global limit would let one visitor clicking around exhaust
    // the whole site's budget and silently 429 every other concurrent visitor's requests too.
    options.AddPolicy("geomarketing", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    // Separate budget from "geomarketing": that one exists to protect INEGI DENUE's external
    // quota, which population-density never touches — it's just an indexed query against our own
    // Postgres. Sharing the same bucket would mean the map's opportunity-analysis click now spends
    // 2 of that budget's 20/min instead of 1, silently halving how many DENUE lookups a visitor
    // gets before falling back to Places. This one exists only to bound load on our own DB.
    options.AddPolicy("population-density", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// ---- CORS (Angular dev server + configurable prod origin) ----
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RealEstate API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Paste a JWT as: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
