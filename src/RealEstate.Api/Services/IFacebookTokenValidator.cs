namespace RealEstate.Api.Services;

public record FacebookProfile(string Id, string? Email, string? FirstName, string? LastName);

public interface IFacebookTokenValidator
{
    // Verifies the access token server-side (never trusts whatever the client claims) and
    // returns the profile Facebook actually has for it. Throws FacebookAuthException for an
    // actually-invalid/expired token or one issued for a different app, and
    // FacebookUnavailableException when Graph API couldn't be reached or timed out — so
    // AuthController can tell "bad token" apart from "Facebook is down" the same way it already
    // does for Google's InvalidJwtException vs. a network failure.
    Task<FacebookProfile> ValidateAsync(string accessToken, string appId, string appSecret);
}

public class FacebookAuthException : Exception
{
    public FacebookAuthException(string message) : base(message) { }
}

public class FacebookUnavailableException : Exception
{
    public FacebookUnavailableException(string message, Exception inner) : base(message, inner) { }
}
