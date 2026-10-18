using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTypeCriteriaCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_type_criteria",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    denue_search_term = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    google_places_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    min_frontage_meters = table.Column<double>(type: "double precision", nullable: false),
                    min_lot_size_sqm = table.Column<double>(type: "double precision", nullable: false),
                    prefers_corner_lot = table.Column<bool>(type: "boolean", nullable: true),
                    prefers_primary_road_access = table.Column<bool>(type: "boolean", nullable: false),
                    population_radius_km = table.Column<double>(type: "double precision", nullable: true),
                    min_population_in_radius = table.Column<int>(type: "integer", nullable: true),
                    competitor_exclusion_radius_meters = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_type_criteria", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "business_type_criteria",
                columns: new[] { "id", "competitor_exclusion_radius_meters", "denue_search_term", "display_name", "google_places_type", "key", "min_frontage_meters", "min_lot_size_sqm", "min_population_in_radius", "notes", "population_radius_km", "prefers_corner_lot", "prefers_primary_road_access" },
                values: new object[,]
                {
                    { 1, 1500, "gasolinera", "Gasolinera", "gas_station", "gas_station", 20.0, 400.0, null, "Basado en reglamentación municipal típica en México (p.ej. Zapopan): 400 m²/20 m de frente en esquina, 800 m²/30 m fuera de esquina — aquí se usa el mínimo de esquina. Verificar siempre con el reglamento del municipio específico.", null, true, true },
                    { 2, 400, "farmacia", "Farmacia", "pharmacy", "pharmacy", 10.0, 250.0, 8000, "Estimación: sin cifra oficial pública de cadenas; 250 m² coincide con una nota de prensa sobre Farmacias Guadalajara, el resto es inferencia de bienes raíces comercial.", 1.5, true, true },
                    { 3, 300, "tienda de conveniencia", "Tienda de conveniencia", "convenience_store", "convenience_store", 8.0, 180.0, null, "Estimación. Prioriza tráfico vehicular/peatonal y visibilidad por encima de densidad poblacional.", null, true, true },
                    { 4, 800, "gimnasio", "Gimnasio", "gym", "gym", 15.0, 500.0, 15000, "Estimación.", 2.0, null, false },
                    { 5, null, "restaurante", "Restaurante", "restaurant", "restaurant", 12.0, 350.0, null, "Estimación.", 1.5, true, true },
                    { 6, 1500, "supermercado", "Supermercado", "supermarket", "supermarket", 40.0, 3000.0, 25000, "Estimación.", 3.0, null, true },
                    { 7, null, "banco", "Sucursal bancaria", "bank", "bank_branch", 10.0, 250.0, null, "Estimación; en la práctica prioriza nivel socioeconómico medio-alto más que población total (nivel socioeconómico aún no se pondera en el score).", 1.5, true, false },
                    { 8, null, "colegio", "Escuela privada", "school", "private_school", 20.0, 1000.0, null, "Estimación; a diferencia de los demás tipos, evita vialidades primarias de alto tráfico por seguridad y ruido.", 2.0, false, false },
                    { 9, null, "bodega", "Bodega / nave industrial", null, "warehouse", 30.0, 3000.0, null, "Estimación; prioriza acceso a vialidad primaria/carretera y uso de suelo industrial, no densidad poblacional.", null, null, true }
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_type_criteria_key",
                table: "business_type_criteria",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_type_criteria");
        }
    }
}
