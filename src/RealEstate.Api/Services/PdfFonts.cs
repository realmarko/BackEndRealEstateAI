using System.Reflection;

namespace RealEstate.Api.Services;

// Shared by every QuestPDF-based service (FactSheetPdfService, ViabilityReportPdfService, ...) so
// the embedded "Liberation Sans" font (see FactSheetPdfService's original doc comment for why it's
// embedded rather than a system font) is only registered once per process regardless of how many
// PDF services end up using it.
public static class PdfFonts
{
    public const string FontFamily = "Liberation Sans";

    private static bool _registered;
    private static readonly object Lock = new();

    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (Lock)
        {
            if (_registered) return;
            RegisterEmbeddedFont("RealEstate.Api.Resources.Fonts.LiberationSans-Regular.ttf");
            RegisterEmbeddedFont("RealEstate.Api.Resources.Fonts.LiberationSans-Bold.ttf");
            _registered = true;
        }
    }

    private static void RegisterEmbeddedFont(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
        QuestPDF.Drawing.FontManager.RegisterFont(stream);
    }
}
