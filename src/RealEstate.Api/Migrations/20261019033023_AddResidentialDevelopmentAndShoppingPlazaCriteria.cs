using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResidentialDevelopmentAndShoppingPlazaCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_primary_road_access" },
                values: new object[,]
                {
                    { 10, "Residencial,Urbano,Urbanizable", null, "fraccionadora", "Desarrollo Inmobiliario Residencial", null, "residential_development", 30.0, 5000.0, null, "Estimación; sin cifra oficial por municipio — superficie/frente mínimos típicos para un fraccionamiento residencial de escala pequeña. No evalúa densidad poblacional ni competencia directa, ya que un desarrollo genera su propia demanda en vez de depender de la existente.", null, null, true },
                    { 11, "Urbano,Comercial", 2000, "centro comercial", "Plaza Comercial", "shopping_mall", "shopping_plaza", 40.0, 5000.0, 20000, "Estimación; prioriza alta visibilidad/acceso vehicular y evita otra plaza o centro comercial cercano que compita por el mismo catchment.", 3.0, true, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 11);
        }
    }
}
