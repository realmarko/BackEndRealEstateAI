namespace RealEstate.Api.Services;

// Where the Angular app is served from — used to build links back into it (e.g. a listing's
// detail page) from server-sent emails. Not tied to Cors:AllowedOrigins on purpose: that list
// can hold multiple origins in production, with no single one being "the" canonical app URL.
public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:4200";
}
