using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSmallCommercialBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_cus_ratio", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_high_traffic", "prefers_primary_road_access" },
                values: new object[,]
                {
                    { 15, "Residencial,Urbano,Urbanizable", null, "fraccionadora", "Casa Unifamiliar", null, "single_family_house", null, 7.0, 120.0, null, "Estimación; dimensiones de mercado típicas para una casa unifamiliar de clase media, sin restricción de precio/ingreso (a diferencia de \"Casa de Interés Social\"). Verificar contra el reglamento municipal específico.", null, null, false, true },
                    { 16, "Urbano,Comercial", null, "local comercial", "Local Comercial", "store", "local_comercial", null, 5.0, 60.0, null, "Estimación; categoría genérica para un local comercial pequeño sin giro específico (60 m²/5 m de frente, por indicación directa del usuario) — para un giro particular, usa la fila específica del catálogo (farmacia, cafetería, etc.) si existe.", null, null, true, true },
                    { 17, "Urbano,Comercial", 250, "cafetería", "Cafetería", "cafe", "cafeteria", null, 6.0, 80.0, 5000, "Estimación; depende de tráfico peatonal/vehicular y visibilidad, similar a una tienda de conveniencia.", 1.0, null, true, true },
                    { 18, "Urbano,Comercial", null, "consultorio médico", "Consultorio", "doctor", "consultorio", null, 5.0, 70.0, 6000, "Estimación; sin radio de exclusión de competencia — la especialidad médica varía demasiado para que un conteo genérico de \"consultorios\" cercanos sea significativo.", 1.5, null, false, true },
                    { 19, "Urbano,Comercial", 250, "salón de belleza", "Estética", "beauty_salon", "estetica", null, 6.0, 70.0, 4000, "Estimación; depende de clientela de paso/visibilidad, similar a una boutique.", 1.0, null, true, true },
                    { 20, "Urbano,Comercial", 500, "spa", "Spa", "spa", "spa", null, 7.0, 100.0, 8000, "Estimación; cliente llega por cita, prioriza ambiente tranquilo sobre tráfico/visibilidad de escaparate.", 2.0, null, false, true },
                    { 21, "Urbano,Comercial", 200, "tienda de conveniencia", "Tienda de Conveniencia Pequeña", "convenience_store", "small_convenience_store", null, 5.0, 60.0, null, "Estimación; formato compacto distinto de \"Tienda de conveniencia\" (más grande) ya en el catálogo — radio de exclusión más chico porque este formato tolera mayor densidad.", null, true, true, true },
                    { 22, "Urbano,Comercial", 200, "boutique", "Boutique", "clothing_store", "boutique", null, 5.0, 60.0, null, "Estimación; depende de tráfico peatonal/visibilidad, típicamente en plaza comercial o corredor comercial.", null, null, true, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 22);
        }
    }
}
