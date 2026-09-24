using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMunicipalBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "municipal_boundaries",
                columns: table => new
                {
                    cvegeo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    boundary = table.Column<Geometry>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipal_boundaries", x => x.cvegeo);
                });

            migrationBuilder.CreateIndex(
                name: "ix_municipal_boundaries_boundary",
                table: "municipal_boundaries",
                column: "boundary")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "municipal_boundaries");
        }
    }
}
