using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IResidentialAnalysisPdfService
{
    byte[] Generate(ListingDto listing, ResidentialAnalysisDto analysis);
}

// House/apartment counterpart to ViabilityReportPdfService — same brand tokens/fonts so it reads
// as the same product, simpler structure since there's one scored comparison per listing rather
// than a per-business-type accordion.
public class ResidentialAnalysisPdfService : IResidentialAnalysisPdfService
{
    private static readonly string Navy = "#1e3a5f";
    private static readonly string Gold = "#c6a15b";
    private const string FontFamily = PdfFonts.FontFamily;

    static ResidentialAnalysisPdfService()
    {
        PdfFonts.EnsureRegistered();
    }

    public byte[] Generate(ListingDto listing, ResidentialAnalysisDto analysis)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => ComposeHeader(c, listing, analysis));

                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(c => ComposeKpiRow(c, analysis));
                    if (analysis.Comparables.Count > 0)
                    {
                        column.Item().Element(c => ComposeComparables(c, analysis));
                    }
                    if (analysis.AppreciationScore is not null || analysis.PopulationDataAvailable)
                    {
                        column.Item().Element(c => ComposeAreaSignals(c, analysis));
                    }
                    if (analysis.RentalYield is { } yield)
                    {
                        column.Item().Element(c => ComposeRentalYield(c, yield));
                    }
                });

                page.Footer().PaddingTop(12).Text(text =>
                {
                    text.Span("Espacial.com.mx — Reporte generado automáticamente a partir de propiedades comparables reales publicadas en el sitio y datos de INEGI (censo de población). No sustituye un avalúo profesional.")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ListingDto listing, ResidentialAnalysisDto analysis)
    {
        container.Background(Navy).Padding(16).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Reporte de Análisis Residencial").FontSize(16).Bold().FontColor(Colors.White);
                col.Item().Text(listing.Title).FontSize(11).FontColor(Gold);
                col.Item().Text($"{listing.Street}, {listing.Colonia}, {listing.City}, {listing.State}").FontSize(9).FontColor(Colors.White);
                col.Item().Text($"{analysis.Lat:0.00000}, {analysis.Lng:0.00000}").FontSize(8).FontColor(Colors.Grey.Lighten2);
            });
        });
    }

    private static void ComposeKpiRow(IContainer container, ResidentialAnalysisDto analysis)
    {
        container.Row(row =>
        {
            row.Spacing(10);
            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "PRECIO POR M² (SUJETO)",
                analysis.SubjectPricePerSqm is { } sp ? $"${sp:N0}" : "—",
                analysis.AvgPricePerSqm is { } avg ? $"Mercado: ${avg:N0}/m²" : null));

            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "COMPARABLES CERCANOS",
                analysis.ComparablesCount.ToString(),
                analysis.PercentileVsComparables is { } pct ? $"Percentil {pct}" : null));

            row.RelativeItem().Element(c => ComposeKpiTile(
                c, "POBLACIÓN CERCANA",
                analysis.PopulationDataAvailable ? $"{analysis.PopulationInRadius:N0}" : "—",
                analysis.PopulationDataAvailable ? "habitantes" : "dato no disponible"));
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

    private static void ComposeComparables(IContainer container, ResidentialAnalysisDto analysis)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Propiedades comparables cercanas").FontSize(11).Bold().FontColor(Navy);
            foreach (var comparable in analysis.Comparables)
            {
                var pricePerSqm = comparable.AreaSqFt > 0 ? comparable.Price / (comparable.AreaSqFt * ResidentialAnalysisService.SqmPerSqFt) : (decimal?)null;
                col.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem(3).Text(comparable.Title).FontSize(9);
                    row.RelativeItem(2).Text($"${comparable.Price:N0}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(80).AlignRight().Text(pricePerSqm is { } p ? $"${p:N0}/m²" : "—").FontSize(9);
                });
            }
        });
    }

    private static void ComposeAreaSignals(IContainer container, ResidentialAnalysisDto analysis)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Señales de la zona").FontSize(11).Bold().FontColor(Navy);
            if (analysis.PopulationDataAvailable)
            {
                col.Item().PaddingTop(3).Text($"Población estimada en la zona censal: {analysis.PopulationInRadius:N0} habitantes.").FontSize(9);
            }
            if (analysis.AppreciationScore is { } score)
            {
                col.Item().PaddingTop(3).Text($"Potencial de plusvalía (2 años): {score:N0}/100.").FontSize(9);
                foreach (var explanation in analysis.AppreciationExplanations)
                {
                    col.Item().PaddingLeft(8).PaddingTop(2).Text($"• {explanation}").FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            }
        });
    }

    private static void ComposeRentalYield(IContainer container, RentalYieldEstimateDto yield)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
        {
            col.Item().Text("Estimación de rendimiento por renta").FontSize(11).Bold().FontColor(Navy);
            col.Item().PaddingTop(3).Text(
                $"Basado en {yield.NearbyRentCount} propiedades en renta cercanas con renta promedio de ${yield.AvgMonthlyRent:N0}/mes, " +
                $"el rendimiento anual estimado es de {yield.EstimatedAnnualYieldPct:N1}%.").FontSize(9);
        });
    }
}
