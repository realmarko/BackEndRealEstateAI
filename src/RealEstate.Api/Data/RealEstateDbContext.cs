using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options) { }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentReview> AgentReviews => Set<AgentReview>();
    public DbSet<Brokerage> Brokerages => Set<Brokerage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Agent>(entity =>
        {
            entity.HasIndex(a => a.UserId).IsUnique().HasFilter("user_id IS NOT NULL");
            entity.Property(a => a.Specialties).HasDefaultValueSql("'{}'");

            // SetNull (not Restrict/Cascade): a brokerage being removed from the catalog should
            // fall the agent back to independent, not block the deletion or delete the agent.
            entity.HasOne(a => a.Brokerage)
                  .WithMany(b => b.Agents)
                  .HasForeignKey(a => a.BrokerageId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AgentReview>(entity =>
        {
            // One review per reviewer per agent.
            entity.HasIndex(r => new { r.AgentId, r.ReviewerUserId }).IsUnique();

            entity.HasOne(r => r.Agent)
                  .WithMany(a => a.Reviews)
                  .HasForeignKey(r => r.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Brokerage>(entity =>
        {
            // Declared here so `dotnet ef migrations add` keeps seeing "unique index on Name"
            // as unchanged and never tries to touch it. The actual index in the database is a
            // case-insensitive one (unique on LOWER(name)), swapped in via raw SQL in the
            // AddAgentBrokerageId migration — EF's fluent API has no first-class way to express
            // a Postgres expression index, and this line can't be safely edited to match without
            // scaffolding a spurious drop/recreate of that functional index.
            entity.HasIndex(b => b.Name).IsUnique();
        });

        builder.Entity<Property>(entity =>
        {
            entity.Property(p => p.Price).HasColumnType("numeric(14,2)");
            entity.HasIndex(p => p.City);
            entity.HasIndex(p => new { p.Latitude, p.Longitude });

            entity.HasOne(p => p.Agent)
                  .WithMany(a => a.Properties)
                  .HasForeignKey(p => p.AgentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Images)
                  .WithOne(i => i.Property)
                  .HasForeignKey(i => i.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(p => p.Amenities)
                  .WithMany(a => a.Properties)
                  .UsingEntity(j => j.ToTable("PropertyAmenities"));
        });
    }
}
