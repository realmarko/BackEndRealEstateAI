namespace RealEstate.Api.Services;

// Powers ListingDraftParsingService's extraction of structured listing fields from a pasted
// classified-ad text (see /api/listings/parse-draft). Paid, billed-per-request API — Claude
// Haiku is cheap enough per call that no caching makes sense here (every pasted text is
// different), but the rate-limit policy on the endpoint still bounds worst-case spend.
public class AnthropicOptions
{
    public string ApiKey { get; set; } = "CHANGE_ME_ANTHROPIC_API_KEY";
}
