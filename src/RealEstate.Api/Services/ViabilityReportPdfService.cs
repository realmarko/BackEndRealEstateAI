using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IViabilityReportPdfService
{
    // mapImage: a static map PNG centered on the listing (IStaticMapService), or null — the
    // caller fetches it (best-effort, may fail/be unconfigured) rather than this service reaching
    // out to Google itself, same separation as FactSheetBuilderService downloading photos before
    // calling FactSheetPdfService.Generate.
    byte[] Generate(ListingDto listing, ViabilityAnalysisDto analysis, byte[]? mapImage = null);
}

// Renders the "Reporte de Viabilidad Comercial" PDF emailed to a lead after they request it on a
// Land/commercial listing (see ListingsController's viability-report endpoint) — same brand
// treatment as FactSheetPdfService (shared Navy/Gold tokens, embedded Liberation Sans) so it reads
// as the same product, built as its own document since the content (business-fit scoring, not
// property specs) is a different shape than the fact sheet's ListingDto-only layout.
public class ViabilityReportPdfService : IViabilityReportPdfService
{
    private static readonly string Navy = "#1e3a5f";
    private static readonly string Gold = "#c6a15b";
    private const string FontFamily = PdfFonts.FontFamily;

    // Ranked highest-fit first — leads with the top matches, which is the whole point of the
    // report (not a generic listing walkthrough like the fact sheet).
    private const int MaxBusinessesShown = 9;

    static ViabilityReportPdfService()
    {
        PdfFonts.EnsureRegistered();
    }

    public byte[] Generate(ListingDto listing, ViabilityAnalysisDto analysis, byte[]? mapImage = null)
    {
        var ranked = analysis.Businesses.OrderByDescending(b => b.Score).Take(MaxBusinessesShown).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, listing));

                var topBusiness = analysis.Businesses.OrderByDescending(b => b.Score).FirstOrDefault();

                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(c => ComposeKpiRow(c, listing, analysis, topBusiness));
                    column.Item().Element(c => ComposeFactSheet(c, listing));
                    if (mapImage is not null)
                    {
                        column.Item().Height(140).Element(c => c.Image(mapImage).FitArea());
                        column.Item().Element(ComposeLegend);
                    }
                    if (analysis.NearbyRoads.Count > 0)
                    {
                        column.Item().Element(c => ComposeNearbyRoads(c, analysis.NearbyRoads));
                    }
                    if (analysis.TrafficGenerators.Count > 0)
                    {
                        column.Item().Element(c => ComposeTrafficGenerators(c, analysis.TrafficGenerators));
                    }
                    foreach (var business in ranked)
                    {
                        column.Item().Element(c => ComposeBusiness(c, business));
                    }
                });

                page.Footer().PaddingTop(12).Text(text =>
                {
                    text.Span("Espacial.com.mx — Reporte generado automáticamente a partir de datos de INEGI (DENUE, censo de población) y de la ficha del predio. Los criterios usados son estimaciones iniciales y pueden ajustarse.")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ListingDto listing)
    {
        container.Background(Navy).Padding(16).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Reporte de Viabilidad Comercial").FontSize(16).Bold().FontColor(Colors.White);
                col.Item().Text(listing.Title).FontSize(11).FontColor(Gold);
                col.Item().Text($"{listing.Street}, {listing.Colonia}, {listing.City}, {listing.State}").FontSize(9).FontColor(Colors.White);
            });
        });
    }

    private static void ComposeKpiRow(IContainer container, ListingDto listing, ViabilityAnalysisDto analysis, BusinessViabilityDto? topBusiness)
    {
        container.Row(row =>
        {
            row.Spacing(10);
            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "SUPERFICIE TOTAL",
                listing.LotSizeSqm is { } lot ? $"{lot:N0} m²" : "—",
                listing.FrontageWidthMeters is { } frontage ? $"Frente: {frontage:0.#} m" : null));

            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "POBLACIÓN CERCANA",
                analysis.PopulationDataAvailable ? $"{analysis.PopulationInRadius:N0}" : "—",
                analysis.PopulationDataAvailable ? "habitantes" : "dato no disponible"));

            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "MEJOR VIABILIDAD",
                topBusiness is not null ? $"{topBusiness.Score}%" : "—",
                topBusiness?.DisplayName));
        });
    }

    private static void ComposeKpiTile(IContainer container, string label, string value, string? sub)
    {
        container.Background("#eff6ff").Border(1).BorderColor("#bfdbfe").Padding(8).Column(col =>
        {
            col.Item().AlignCenter().Text(label).FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
            col.Item().AlignCenter().Text(value).FontSize(16).Bold().FontColor(Navy);
            if (sub is not null)
            {
                col.Item().AlignCenter().Text(sub).FontSize(7).FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeFactSheet(IContainer container, ListingDto listing)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Ficha técnica del predio").FontSize(11).Bold().FontColor(Navy);

            ComposeFactRow(col, "Tipología", listing.IsCornerLot is { } corner ? (corner ? "Esquina" : "No es esquina") : "—");
            ComposeFactRow(col, "Estatus", listing.Topography is { } topo ? (topo == "Plana" ? "Terreno plano" : "Terreno inclinado") : "—");
            ComposeFactRow(col, "Acceso", listing.HasPavedStreet is { } paved ? (paved ? "Calle pavimentada" : "Sin pavimentar") : "—");
        });
    }

    private static void ComposeFactRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingTop(3).Row(row =>
        {
            row.ConstantItem(90).Text(label).FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
            row.RelativeItem().Text(value).FontSize(9);
        });
    }

    // Mirrors the interactive page's own map-legend panel (same categories/colors) — the PDF's
    // static map plots the same markers/road lines, so it needs the same key explaining them.
    private static void ComposeLegend(IContainer container)
    {
        (string Label, string Color)[] markers =
        {
            ("Escuela", "#15803d"), ("Centro comercial", "#c2410c"), ("Hospital", "#b91c1c"), ("Transporte público", "#2563eb")
        };
        (string Label, string Color)[] roads =
        {
            ("Autopista", "#7f1d1d"), ("Vía rápida", "#c2410c"), ("Avenida principal", "#dc2626"), ("Avenida secundaria", "#16a34a")
        };

        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
        {
            row.Spacing(14);
            foreach (var (label, color) in markers)
            {
                row.AutoItem().Row(r =>
                {
                    r.ConstantItem(8).Height(8).Background(color);
                    r.ConstantItem(4);
                    r.AutoItem().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                });
            }
            foreach (var (label, color) in roads)
            {
                row.AutoItem().Row(r =>
                {
                    r.ConstantItem(12).Height(3).Background(color);
                    r.ConstantItem(4);
                    r.AutoItem().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                });
            }
        });
    }

    private static void ComposeNearbyRoads(IContainer container, List<NearbyRoadDto> roads)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Vialidades cercanas").FontSize(11).Bold().FontColor(Navy);
            foreach (var road in roads)
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem(3).Text(road.Name ?? road.Classification).FontSize(9);
                    row.RelativeItem(2).Text(road.Classification).FontSize(9).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(60).AlignRight().Text($"{road.DistanceMeters:N0} m").FontSize(9);
                });
            }
        });
    }

    private static void ComposeTrafficGenerators(IContainer container, List<TrafficGeneratorDto> generators)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Generadores de tráfico cercanos").FontSize(11).Bold().FontColor(Navy);
            foreach (var generator in generators)
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem(3).Text(generator.Name).FontSize(9);
                    row.RelativeItem(2).Text(generator.Category).FontSize(9).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(60).AlignRight().Text($"{generator.DistanceMeters:N0} m").FontSize(9);
                });
            }
        });
    }

    private static void ComposeBusiness(IContainer container, BusinessViabilityDto business)
    {
        var scoreColor = business.Score >= 70 ? "#15803d" : business.Score >= 40 ? "#b45309" : "#b91c1c";

        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(business.DisplayName).FontSize(12).Bold().FontColor(Navy);
                row.ConstantItem(60).AlignRight().Text($"{business.Score}%").FontSize(14).Bold().FontColor(scoreColor);
            });

            foreach (var check in business.Checks)
            {
                col.Item().PaddingTop(3).Row(row =>
                {
                    var mark = check.Passed is null ? "•" : check.Passed.Value ? "✓" : "✗";
                    var markColor = check.Passed is null ? "#9ca3af" : check.Passed.Value ? "#15803d" : "#b91c1c";
                    row.ConstantItem(16).Text(mark).FontColor(markColor).Bold();
                    row.RelativeItem().Text(check.Detail).FontSize(9);
                });
            }

            if (business.ScoreCapReason is not null)
            {
                col.Item().PaddingTop(4).Text(business.ScoreCapReason).FontSize(8).Italic().FontColor("#b91c1c");
            }

            if (business.NearbyCompetitors.Count > 0)
            {
                col.Item().PaddingTop(6).Text("Competidores cercanos:").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2);
                foreach (var competitor in business.NearbyCompetitors)
                {
                    col.Item().PaddingLeft(8).Text($"{competitor.Name} — {competitor.DistanceMeters:N0} m").FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            }
        });
    }
}
