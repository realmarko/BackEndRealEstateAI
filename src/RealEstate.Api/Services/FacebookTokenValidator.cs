using System.Net.Http.Json;
using System.Text.Json;

namespace RealEstate.Api.Services;

// Facebook has no server-side SDK equivalent to Google.Apis.Auth's GoogleJsonWebSignature, so
// this calls the Graph API directly: debug_token first to confirm the access token is valid and
// was actually issued for OUR app (never trust a token just because the client sent one), then
// /me to read the profile Facebook has for it.
public class FacebookTokenValidator : IFacebookTokenValidator
{
    private readonly HttpClient _httpClient;

    public FacebookTokenValidator(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FacebookProfile> ValidateAsync(string accessToken, string appId, string appSecret)
    {
        var appAccessToken = $"{appId}|{appSecret}";
        JsonElement debugData;
        try
        {
            using var debugResponse = await _httpClient.GetAsync(
                $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}" +
                $"&access_token={Uri.EscapeDataString(appAccessToken)}");
            using var debugDoc = await debugResponse.Content.ReadFromJsonAsync<JsonDocument>()
                ?? throw new FacebookAuthException("Empty response from Facebook while verifying the token.");
            if (!debugDoc.RootElement.TryGetProperty("data", out var debugDataRaw))
                throw new FacebookAuthException("Unexpected response from Facebook while verifying the token.");
            // Must clone: debugDoc (declared with `using`) is disposed at the end of this try
            // block, and a JsonElement obtained from it throws ObjectDisposedException on any
            // access after that — Clone() detaches it into its own independent buffer.
            debugData = debugDataRaw.Clone();
        }
        // HttpClient.Timeout (configured in Program.cs) surfaces as TaskCanceledException, not
        // HttpRequestException — both must be caught here, or a slow/unreachable Graph API
        // escapes as an unhandled exception instead of the graceful 502 AuthController.Facebook
        // returns for FacebookUnavailableException.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FacebookUnavailableException("Could not reach Facebook to verify the token.", ex);
        }

        var isValid = debugData.TryGetProperty("is_valid", out var isValidProp) && isValidProp.GetBoolean();
        var tokenAppId = debugData.TryGetProperty("app_id", out var appIdProp) ? appIdProp.GetString() : null;
        if (!isValid || tokenAppId != appId)
            throw new FacebookAuthException("Invalid Facebook access token.");

        JsonElement profile;
        try
        {
            using var meResponse = await _httpClient.GetAsync(
                "https://graph.facebook.com/me?fields=id,email,first_name,last_name" +
                $"&access_token={Uri.EscapeDataString(accessToken)}");
            using var meDoc = await meResponse.Content.ReadFromJsonAsync<JsonDocument>()
                ?? throw new FacebookAuthException("Empty response from Facebook while fetching the profile.");
            profile = meDoc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new FacebookUnavailableException("Could not reach Facebook to fetch the profile.", ex);
        }

        var id = profile.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        if (id is null)
            throw new FacebookAuthException("Facebook did not return a profile for this token.");

        return new FacebookProfile(
            id,
            profile.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null,
            profile.TryGetProperty("first_name", out var firstNameProp) ? firstNameProp.GetString() : null,
            profile.TryGetProperty("last_name", out var lastNameProp) ? lastNameProp.GetString() : null);
    }
}
