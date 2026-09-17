using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyAgebPopulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ageb_code",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "locality_code",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "municipality_code",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "state_code",
                table: "ageb_populations");

            // Every row imported so far is from the 2020 census — backfills existing Puebla rows
            // correctly instead of leaving them at the type's default of 0.
            migrationBuilder.AddColumn<int>(
                name: "census_year",
                table: "ageb_populations",
                type: "integer",
                nullable: false,
                defaultValue: 2020);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "census_year",
                table: "ageb_populations");

            migrationBuilder.AddColumn<string>(
                name: "ageb_code",
                table: "ageb_populations",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "locality_code",
                table: "ageb_populations",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "municipality_code",
                table: "ageb_populations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "state_code",
                table: "ageb_populations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");
        }
    }
}
