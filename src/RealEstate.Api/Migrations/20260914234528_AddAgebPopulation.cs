using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgebPopulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "ageb_populations",
                columns: table => new
                {
                    cvegeo = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    municipality_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    locality_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ageb_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    population = table.Column<int>(type: "integer", nullable: false),
                    area_sq_km = table.Column<double>(type: "double precision", nullable: false),
                    boundary = table.Column<Geometry>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ageb_populations", x => x.cvegeo);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ageb_populations_boundary",
                table: "ageb_populations",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ageb_populations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
