namespace RealEstate.Api.Services;

// The OAuth 2.0 Client ID registered in Google Cloud Console for this app — not a secret itself
// (it's embedded in the frontend bundle too), but kept out of git like every other external
// service credential here; AuthController validates every Google ID token's audience against
// this value.
public class GoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
}
