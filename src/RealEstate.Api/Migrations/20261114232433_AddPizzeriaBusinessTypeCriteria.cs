using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPizzeriaBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DenueSearchTerm is the giro/activity term ("pizza"), not a brand name — DENUE's free-
            // text search already matches any real establishment whose name or activity contains
            // it, so this covers Little Caesars, Domino's, Pizza Hut, and independent pizzerías
            // alike without hardcoding any one chain.
            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "acceptable_land_use_categories", "benefits_from_nearby_residential_growth", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_cus_ratio", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_high_traffic", "prefers_primary_road_access" },
                values: new object[,]
                {
                    { 23, "Urbano,Comercial", true, 800, "pizza", "Pizzería", "meal_takeaway", "pizzeria", null, 6.0, 80.0, 6000, "Estimación; dimensiones de local similar a Cafetería (local pequeño, alto flujo de pedidos para llevar/entrega). Radio de exclusión de competencia más amplio que un café (800 m) porque el negocio compite por zona de reparto/delivery, no solo por clientela de paso. Incluye cadenas (Little Caesars, Domino's, Pizza Hut) y pizzerías independientes por igual — DENUE se busca por giro (\"pizza\"), no por marca.", 1.5, null, true, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 23);
        }
    }
}
