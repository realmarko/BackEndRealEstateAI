using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficeBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_cus_ratio", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_primary_road_access" },
                values: new object[] { 13, "Urbano,Comercial", null, "oficina", "Oficina", null, "office", null, 12.0, 300.0, null, "Estimación; prioriza accesibilidad vehicular y visibilidad desde vialidad primaria (clientes y empleados) por encima de densidad poblacional residencial cercana.", 2.0, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 13);
        }
    }
}
