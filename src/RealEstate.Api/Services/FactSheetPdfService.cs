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
    private static readonly string Navy = Colors.Blue.Darken4;
    private static readonly string Gold = "#F2A71B";

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

    public byte[] Generate(ListingDto listing, IReadOnlyList<byte[]> photos)
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
                    column.Item().Element(c => ComposeHeader(c, listing));
                    column.Item().Element(c => ComposeStatBand(c, listing));
                    if (shownPhotos.Count > 0) column.Item().Element(c => ComposePhotos(c, shownPhotos));
                    column.Item().PaddingHorizontal(24).PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem(3).Element(c => ComposeDetails(c, listing));
                        row.ConstantItem(16);
                        row.RelativeItem(2).Element(c => ComposeSidePanel(c, listing));
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

    private static void ComposeHeader(IContainer container, ListingDto listing)
    {
        container.Background(Navy).Padding(24).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("FICHA TÉCNICA").FontSize(12).FontColor(Gold).Bold().LetterSpacing(0.1f);
                col.Item().PaddingTop(4).Text(listing.Title).FontSize(20).FontColor(Colors.White).Bold();
                col.Item().PaddingTop(2).Text($"{listing.Street}, {listing.Colonia}, {listing.City}, {listing.State}")
                    .FontSize(10).FontColor(Colors.Grey.Lighten2);
            });
        });
    }

    private static void ComposeStatBand(IContainer container, ListingDto listing)
    {
        container.Background(Colors.Grey.Darken2).Padding(16).Row(row =>
        {
            Stat(row, $"$ {listing.Price:N0} {listing.Currency}", listing.ListingType == "Rent" ? "Renta mensual" : "Precio de venta");
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
            if (listing.Floors is { } floors) Field(column, "Niveles", floors.ToString());
            if (listing.LotSizeSqm is { } lot) Field(column, "Tamaño del terreno", $"{lot:N0} m²");
            if (listing.GardenSizeSqm is { } garden) Field(column, "Jardín", $"{garden:N0} m²");
            if (listing.HasHeatingCooling) Field(column, "Calefacción / A.A.", "Sí");
            if (listing.HasRoofGarden) Field(column, "Roof garden", "Sí");
            if (listing.HoaFee is { } hoa and > 0) Field(column, "Cuota de mantenimiento", $"{listing.Currency} {hoa:N0}");

            if (HasLandDetails(listing))
            {
                column.Item().PaddingTop(12);
                SectionTitle(column, "USO DE SUELO Y SERVICIOS");
                if (!string.IsNullOrWhiteSpace(listing.LandUseZoning)) Field(column, "Zonificación", listing.LandUseZoning!);
                if (!string.IsNullOrWhiteSpace(listing.LandTenure)) Field(column, "Tenencia", listing.LandTenure!);
                if (listing.FrontageWidthMeters is { } w) Field(column, "Frente", $"{w:N1} m");
                if (listing.FrontageDepthMeters is { } d) Field(column, "Fondo", $"{d:N1} m");
                Field(column, "Agua potable", YesNo(listing.HasPotableWater));
                Field(column, "Drenaje", YesNo(listing.HasDrainage));
                Field(column, "Electricidad", YesNo(listing.HasElectricity));
                if (listing.IsFreeOfLiens is { } free) Field(column, "Libre de gravámenes", YesNo(free));
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
        l.HasPotableWater.HasValue || l.HasDrainage.HasValue || l.HasElectricity.HasValue ||
        l.FrontageWidthMeters.HasValue || l.FrontageDepthMeters.HasValue;

    private static string YesNo(bool? value) => value switch { true => "Sí", false => "No", null => "N/D" };

    private static void ComposeSidePanel(IContainer container, ListingDto listing)
    {
        container.Background(Gold).Padding(16).Column(column =>
        {
            column.Item().Text("CONTACTO").FontSize(11).Bold().FontColor(Navy);
            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Navy);
            column.Item().PaddingTop(8).Text(listing.OwnerName).FontSize(11).Bold();
            if (!string.IsNullOrWhiteSpace(listing.OwnerCompany))
                column.Item().Text(listing.OwnerCompany!).FontSize(9);
            column.Item().PaddingTop(4).Text(listing.OwnerEmail).FontSize(9);

            column.Item().PaddingTop(16).LineHorizontal(1).LineColor(Navy);
            column.Item().PaddingTop(8).Text("UBICACIÓN").FontSize(11).Bold().FontColor(Navy);
            column.Item().PaddingTop(4).Text(listing.Street).FontSize(9);
            column.Item().Text($"{listing.Colonia}, {listing.City}").FontSize(9);
            column.Item().Text($"{listing.State}, {listing.ZipCode}").FontSize(9);
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
        container.Padding(16).AlignCenter()
            .Text("Documento generado automáticamente — la información puede cambiar sin previo aviso.")
            .FontSize(7).FontColor(Colors.Grey.Medium);
}
