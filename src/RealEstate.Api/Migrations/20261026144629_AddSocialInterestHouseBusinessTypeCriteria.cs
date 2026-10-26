using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialInterestHouseBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_cus_ratio", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_high_traffic", "prefers_primary_road_access" },
                values: new object[] { 14, "Residencial,Urbano,Urbanizable", null, "fraccionadora", "Casa de Interés Social", null, "social_interest_house", null, 6.0, 50.0, 5000, "Estimación; no hay mínimo federal oficial de m² de terreno/frente (CONAVI clasifica \"interés social\" por precio/ingreso, no por dimensiones) — 50 m²/6 m de frente, ajustado por el usuario hacia el extremo compacto del rango observado (~67-120 m² es lo típico). Verificar contra el reglamento municipal. El radio de población aproxima la política de Perímetros de Contención Urbana (vivienda cerca de empleo/transporte), no es el polígono oficial.", 1.5, null, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 14);
        }
    }
}
