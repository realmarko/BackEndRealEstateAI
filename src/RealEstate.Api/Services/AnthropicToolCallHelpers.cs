using System.Text.Json;

namespace RealEstate.Api.Services;

// Shared by every service that force-calls a single Anthropic tool and reads its JSON input back
// (ListingDraftParsingService, MapSearchParsingService) — extracted once a second call site needed
// the exact same "find the tool_use block, then read its fields defensively" logic.
internal static class AnthropicToolCallHelpers
{
    // Walks the Messages API response for the forced tool's input object. Throws
    // InvalidOperationException (not a parse failure) when the shape is missing entirely — the
    // caller's own catch block maps that to a 502, same convention as every external lookup here.
    public static JsonElement ExtractToolInput(JsonDocument document, string toolName)
    {
        if (!document.RootElement.TryGetProperty("content", out var contentBlocks))
        {
            throw new InvalidOperationException("Anthropic response is missing \"content\".");
        }

        foreach (var block in contentBlocks.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "tool_use"
                && block.TryGetProperty("name", out var nameEl) && nameEl.GetString() == toolName
                && block.TryGetProperty("input", out var input))
            {
                return input;
            }
        }

        throw new InvalidOperationException($"Anthropic response had no \"{toolName}\" tool_use block.");
    }

    // Reads every field defensively — the model is forced to use the tool, but nothing guarantees
    // it populates every property the schema describes, and a field it got wrong (an enum value
    // outside the given list, a non-numeric price) should just come back null rather than fail the
    // whole parse.
    public static string? GetString(JsonElement element, string property, int? maxLength = null)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString();
        return maxLength is { } max && text is { Length: > 0 } && text.Length > max ? text[..max] : text;
    }

    public static bool? GetBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    // TryGetInt32 only succeeds for a JSON number token with no fractional part (e.g. "3", not
    // "3.0") — nothing guarantees the model emits integer-typed fields without a trailing ".0"
    // just because the tool schema says "integer", so this falls back to rounding a decimal
    // instead of silently dropping an otherwise-valid value.
    public static int? GetInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        if (value.TryGetInt32(out var i)) return i;
        return value.TryGetDecimal(out var d) ? (int)Math.Round(d) : null;
    }

    public static decimal? GetDecimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var d)
            ? d
            : null;

    // enum values must exactly match the corresponding C# enum member names (Entities/Enums.cs) —
    // parsed case-insensitively via Enum.TryParse.
    public static TEnum? GetEnum<TEnum>(JsonElement element, string property) where TEnum : struct, Enum =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
        && Enum.TryParse<TEnum>(value.GetString(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
}
