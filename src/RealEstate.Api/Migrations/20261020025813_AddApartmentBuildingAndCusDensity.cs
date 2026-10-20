using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentBuildingAndCusDensity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "min_cus_ratio",
                table: "business_type_criteria",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 1,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 2,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 3,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 4,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 5,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 6,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 7,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 8,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 9,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "display_name", "min_cus_ratio", "notes" },
                values: new object[] { "Fraccionamiento Residencial", null, "Estimación; sin cifra oficial por municipio — superficie/frente mínimos típicos para un fraccionamiento residencial de escala pequeña (lotificación horizontal, no un edificio). No evalúa densidad poblacional ni competencia directa, ya que un desarrollo genera su propia demanda en vez de depender de la existente." });

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 11,
                column: "min_cus_ratio",
                value: null);

            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_cus_ratio", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_primary_road_access" },
                values: new object[] { 12, "Urbano,Comercial,Residencial", null, "edificio de departamentos", "Edificio de Departamentos", null, "apartment_building", 2.0, 15.0, 500.0, null, "Estimación; depende fuertemente del CUS (coeficiente de utilización de suelo) registrado para el predio — sin ese dato no se puede estimar cuántos m² de construcción (y por tanto cuántas unidades) permite el terreno.", null, null, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DropColumn(
                name: "min_cus_ratio",
                table: "business_type_criteria");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10,
                columns: new[] { "display_name", "notes" },
                values: new object[] { "Desarrollo Inmobiliario Residencial", "Estimación; sin cifra oficial por municipio — superficie/frente mínimos típicos para un fraccionamiento residencial de escala pequeña. No evalúa densidad poblacional ni competencia directa, ya que un desarrollo genera su propia demanda en vez de depender de la existente." });
        }
    }
}
