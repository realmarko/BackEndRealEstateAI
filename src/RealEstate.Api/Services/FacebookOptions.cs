namespace RealEstate.Api.Services;

// Facebook App ID/Secret registered at developers.facebook.com for this app — unlike Google's
// ClientId, the AppSecret here IS a real secret (used server-side to verify an access token's
// app_id via Graph API's debug_token), so it belongs in user-secrets like every other credential,
// not just kept out of git for tidiness.
public class FacebookOptions
{
    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}
