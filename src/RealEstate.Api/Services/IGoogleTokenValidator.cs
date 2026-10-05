using Google.Apis.Auth;

namespace RealEstate.Api.Services;

// Thin seam around Google.Apis.Auth's static GoogleJsonWebSignature.ValidateAsync — same reason
// ITokenService/IEmailService exist as interfaces here: a static call into an external library
// can't be mocked by AuthControllerTests, an interface can.
public interface IGoogleTokenValidator
{
    // Throws InvalidJwtException (from Google.Apis.Auth) for a malformed/expired/wrong-audience
    // token — callers don't need a separate bool/Result wrapper, that exception type already
    // says everything a caller needs to map to "reject this sign-in attempt."
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string clientId);
}
