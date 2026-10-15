using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

// Renders the one-page "ficha técnica" PDF emailed to a qualifying lead right after they submit
// an inquiry (InquiriesController.Create). Built directly from ListingDto — the same shape the
// frontend's listing-detail page renders from — so the PDF never drifts from what's shown online.
public class FactSheetPdfService : IFactSheetPdfService
{
    // Matches the site's own design tokens (frontend styles.css :root) exactly, not QuestPDF's
    // named palette — this document should look like it came from the same place as the website,
    // not from a generic PDF-report default.
    private static readonly string Navy = "#1e3a5f";
    private static readonly string Gold = "#c6a15b";
    // The navbar's actual background (navbar.component.css) — used only for the brand bar below,
    // which is meant to read as "the site's masthead," not the listing-specific navy used
    // everywhere else in this document.
    private static readonly string BrandBlack = "#0d0d0d";

    // "Liberation Sans" (metric-compatible with Arial), embedded from Resources/Fonts rather than
    // relied on as a system font: the EB Linux host has no fonts installed beyond what
    // .platform/hooks/predeploy/01_install_fontconfig.sh adds (the fontconfig library itself, not
    // any actual typeface), so pointing SkiaSharp at a system "Arial" would work in local dev and
    // then fail or silently substitute a different font in production.
    private const string FontFamily = "Liberation Sans";

    static FactSheetPdfService()
    {
        RegisterEmbeddedFont("RealEstate.Api.Resources.Fonts.LiberationSans-Regular.ttf");
        RegisterEmbeddedFont("RealEstate.Api.Resources.Fonts.LiberationSans-Bold.ttf");
    }

    private static void RegisterEmbeddedFont(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
        QuestPDF.Drawing.FontManager.RegisterFont(stream);
    }

    // Capped at 3 — this is a one-page document, not a gallery, and each photo is a full-size
    // (already server-resized, up to ~1920px) JPEG that adds directly to the email's attachment
    // size. Public so InquiriesController.DownloadPhotosAsync can cap how many it bothers
    // fetching at the same number, instead of hardcoding its own copy of "3".
    public const int MaxPhotos = 3;

    public byte[] Generate(ListingDto listing, IReadOnlyList<byte[]> photos, byte[]? ownerPhoto = null)
    {
        var shownPhotos = photos.Take(MaxPhotos).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Content().Column(column =>
                {
                    column.Item().Element(ComposeBrandHeader);
                    column.Item().Element(c => ComposeHeader(c, listing));
                    column.Item().Element(c => ComposeStatBand(c, listing));
                    if (shownPhotos.Count > 0) column.Item().Element(c => ComposePhotos(c, shownPhotos));
                    column.Item().PaddingHorizontal(24).PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem(3).Element(c => ComposeDetails(c, listing));
                        row.ConstantItem(16);
                        row.RelativeItem(2).Element(c => ComposeSidePanel(c, listing, ownerPhoto));
                    });
                    column.Item().PaddingTop(16).Element(ComposeFooter);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposePhotos(IContainer container, List<byte[]> photos)
    {
        container.Padding(24).PaddingBottom(0).Row(row =>
        {
            foreach (var photo in photos)
            {
                row.RelativeItem().Height(140).Padding(2).Element(c => c.Image(photo).FitArea());
            }
        });
    }

    // The site's own masthead look (navbar.component.css: near-black background, gold wordmark)
    // at the top of the document — a lead shown this PDF should recognize it as coming from
    // Espacial.com.mx at a glance, not just read it in the footer's fine print.
    private static void ComposeBrandHeader(IContainer container) =>
        container.Background(BrandBlack).Padding(10).Row(row =>
        {
            row.RelativeItem().Text("Espacial.com.mx").FontSize(13).Bold().FontColor(Gold);
            row.ConstantItem(120).AlignRight().Text("FICHA TÉCNICA").FontSize(8).FontColor(Colors.Grey.Lighten1).LetterSpacing(0.1f);
        });

    private static void ComposeHeader(IContainer container, ListingDto listing)
    {
        container.Background(Navy).Padding(24).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(listing.Title).FontSize(20).FontColor(Colors.White).Bold();
                col.Item().PaddingTop(2).Text($"{listing.Street}, {listing.Colonia}, {listing.City}, {listing.State}")
                    .FontSize(10).FontColor(Colors.Grey.Lighten2);
            });
        });
    }

    private static void ComposeStatBand(IContainer container, ListingDto listing)
    {
        container.Background(Colors.Grey.Darken2).Padding(16).Row(row =>
        {
            var priceLabel = listing.PriceOnRequest || listing.Price <= 0
                ? "Precio a consultar"
                : $"$ {listing.Price:N0} {listing.Currency}";
            Stat(row, priceLabel, listing.ListingType == "Rent" ? "Renta mensual" : "Precio de venta");
            if (listing.Bedrooms > 0) Stat(row, listing.Bedrooms.ToString(), "Recámaras");
            if (listing.Bathrooms > 0) Stat(row, listing.Bathrooms.ToString("0.#"), "Baños");
            if (listing.AreaSqFt > 0) Stat(row, $"{listing.AreaSqFt:N0}", "Pies² de construcción");
            if (listing.ParkingSpaces is > 0) Stat(row, listing.ParkingSpaces.ToString()!, "Estacionamientos");
        });
    }

    private static void Stat(RowDescriptor row, string value, string label)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().Text(value).FontSize(16).FontColor(Colors.White).Bold();
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeDetails(IContainer container, ListingDto listing)
    {
        container.Column(column =>
        {
            SectionTitle(column, "DETALLES DE LA PROPIEDAD");
            Field(column, "Tipo de propiedad", listing.PropertyType);
            Field(column, "Tipo de operación", listing.ListingType == "Rent" ? "Renta" : "Venta");
            if (listing.YearBuilt is { } year) Field(column, "Año de construcción", year.ToString());
            if (!string.IsNullOrWhiteSpace(listing.ConstructionCondition)) Field(column, "Estado de conservación", listing.ConstructionCondition!);
            if (listing.Floors is { } floors) Field(column, "Niveles", floors.ToString());
            if (listing.ParkingSpaces is { } parking and > 0)
                Field(column, "Estacionamiento", listing.ParkingIsCovered switch { true => $"{parking} (techado)", false => $"{parking} (descubierto)", null => parking.ToString() });
            if (!string.IsNullOrWhiteSpace(listing.FacadeOrientation)) Field(column, "Orientación de fachada", listing.FacadeOrientation!);
            if (listing.LotSizeSqm is { } lot) Field(column, "Tamaño del terreno", $"{lot:N0} m²");
            if (listing.GardenSizeSqm is { } garden) Field(column, "Jardín", $"{garden:N0} m²");
            if (listing.HasHeatingCooling) Field(column, "Calefacción / A.A.", "Sí");
            if (listing.HasRoofGarden) Field(column, "Roof garden", "Sí");
            if (listing.HasNaturalGas is { } naturalGas) Field(column, "Gas natural", YesNo(naturalGas));
            if (listing.HasLpGas is { } lpGas) Field(column, "Gas estacionario", YesNo(lpGas));
            if (listing.HoaFee is { } hoa and > 0) Field(column, "Cuota de mantenimiento", $"{listing.Currency} {hoa:N0}");
            if (IsHttpUrl(listing.VideoTourUrl))
            {
                column.Item().PaddingTop(4).Row(row =>
                {
                    row.ConstantItem(160).Text("Recorrido virtual").FontSize(9).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().Hyperlink(listing.VideoTourUrl!).Text(listing.VideoTourUrl!).FontSize(9).Bold();
                });
            }

            if (HasLandDetails(listing))
            {
                column.Item().PaddingTop(12);
                SectionTitle(column, "USO DE SUELO Y SERVICIOS");
                if (!string.IsNullOrWhiteSpace(listing.LandUseZoning)) Field(column, "Zonificación", listing.LandUseZoning!);
                if (!string.IsNullOrWhiteSpace(listing.LandTenure)) Field(column, "Tenencia", listing.LandTenure!);
                if (listing.HasEjidalCertificate is { } ejidal) Field(column, "Constancia ejidal", YesNo(ejidal));
                if (listing.FrontageWidthMeters is { } w) Field(column, "Frente", $"{w:N1} m");
                if (listing.FrontageDepthMeters is { } d) Field(column, "Fondo", $"{d:N1} m");
                if (!string.IsNullOrWhiteSpace(listing.LotShape)) Field(column, "Forma del terreno", listing.LotShape!);
                if (!string.IsNullOrWhiteSpace(listing.Topography)) Field(column, "Topografía", listing.Topography!);
                if (listing.CosCoefficient is { } cos) Field(column, "COS", cos.ToString("0.##"));
                if (listing.CusCoefficient is { } cus) Field(column, "CUS", cus.ToString("0.##"));
                Field(column, "Agua potable", YesNo(listing.HasPotableWater));
                Field(column, "Drenaje", YesNo(listing.HasDrainage));
                Field(column, "Electricidad", YesNo(listing.HasElectricity));
                if (listing.HasTelecomService is { } telecom) Field(column, "Conectividad / fibra", YesNo(telecom));
                if (listing.HasPavedStreet is { } paved) Field(column, "Calle pavimentada", YesNo(paved));
                if (listing.IsFreeOfLiens is { } free) Field(column, "Libre de gravámenes", YesNo(free));
                if (listing.HasPropertyTaxDebt is { } taxDebt) Field(column, "Adeudo de predial", YesNo(taxDebt));
                if (listing.HasWaterDebt is { } waterDebt) Field(column, "Adeudo de agua", YesNo(waterDebt));
            }

            if (!string.IsNullOrWhiteSpace(listing.Description))
            {
                column.Item().PaddingTop(12);
                SectionTitle(column, "DESCRIPCIÓN");
                column.Item().PaddingTop(4).Text(listing.Description).FontSize(9).LineHeight(1.3f);
            }
        });
    }

    private static bool HasLandDetails(ListingDto l) =>
        !string.IsNullOrWhiteSpace(l.LandUseZoning) || !string.IsNullOrWhiteSpace(l.LandTenure) ||
        l.HasEjidalCertificate.HasValue ||
        l.HasPotableWater.HasValue || l.HasDrainage.HasValue || l.HasElectricity.HasValue ||
        l.FrontageWidthMeters.HasValue || l.FrontageDepthMeters.HasValue ||
        !string.IsNullOrWhiteSpace(l.LotShape) || !string.IsNullOrWhiteSpace(l.Topography) ||
        l.CosCoefficient.HasValue || l.CusCoefficient.HasValue ||
        l.HasTelecomService.HasValue || l.HasPavedStreet.HasValue ||
        l.HasPropertyTaxDebt.HasValue || l.HasWaterDebt.HasValue;

    private static string YesNo(bool? value) => value switch { true => "Sí", false => "No", null => "N/D" };

    // VideoTourUrl only has a [MaxLength] constraint server-side (the frontend's own http(s)-only
    // validation is client-side, bypassable by calling the API directly) — guarded here before
    // treating it as a clickable PDF link so a non-http scheme (javascript:, file:, a custom
    // protocol) can never end up in a Hyperlink annotation that some PDF viewer hands to the OS.
    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static void ComposeSidePanel(IContainer container, ListingDto listing, byte[]? ownerPhoto)
    {
        container.Background(Gold).Padding(16).Column(column =>
        {
            column.Item().Text("CONTACTO").FontSize(11).Bold().FontColor(Navy);
            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Navy);
            column.Item().PaddingTop(8).Row(row =>
            {
                if (ownerPhoto is { Length: > 0 })
                    row.ConstantItem(40).Height(40).Image(ownerPhoto).FitArea();
                row.RelativeItem().PaddingLeft(ownerPhoto is { Length: > 0 } ? 8 : 0).Column(col =>
                {
                    col.Item().Text(listing.OwnerName).FontSize(11).Bold();
                    if (!string.IsNullOrWhiteSpace(listing.OwnerCompany))
                        col.Item().Text(listing.OwnerCompany!).FontSize(9);
                });
            });
            column.Item().PaddingTop(4).Text(listing.OwnerEmail).FontSize(9);

            column.Item().PaddingTop(16).LineHorizontal(1).LineColor(Navy);
            column.Item().PaddingTop(8).Text("UBICACIÓN").FontSize(11).Bold().FontColor(Navy);
            column.Item().PaddingTop(4).Text(listing.Street).FontSize(9);
            column.Item().Text($"{listing.Colonia}, {listing.City}").FontSize(9);
            column.Item().Text($"{listing.State}, {listing.ZipCode}").FontSize(9);
            if (listing.Latitude != 0 || listing.Longitude != 0)
                column.Item().PaddingTop(2).Text($"{listing.Latitude:F6}, {listing.Longitude:F6}").FontSize(8).FontColor(Colors.Grey.Darken3);
        });
    }

    private static void SectionTitle(ColumnDescriptor column, string text) =>
        column.Item().BorderBottom(1).BorderColor(Navy).PaddingBottom(4)
            .Text(text).FontSize(11).Bold().FontColor(Navy);

    private static void Field(ColumnDescriptor column, string label, string value) =>
        column.Item().PaddingTop(4).Row(row =>
        {
            row.ConstantItem(160).Text(label).FontSize(9).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().Text(value).FontSize(9).Bold();
        });

    private static void ComposeFooter(IContainer container) =>
        container.PaddingHorizontal(24).PaddingBottom(16).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(10).AlignCenter().Text(text =>
            {
                text.Span("Espacial.com.mx").FontSize(8).Bold().FontColor(Navy);
                text.Span(" — encuentra y publica propiedades en México").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            column.Item().PaddingTop(2).AlignCenter()
                .Text("Documento generado automáticamente — la información puede cambiar sin previo aviso.")
                .FontSize(7).FontColor(Colors.Grey.Medium);
        });
}
