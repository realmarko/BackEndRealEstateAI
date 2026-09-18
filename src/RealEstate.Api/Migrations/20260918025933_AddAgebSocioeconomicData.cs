using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgebSocioeconomicData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "avg_occupants_per_home",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "avg_schooling_years",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estimated_socioeconomic_level",
                table: "ageb_populations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pct_homes_with_car",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pct_homes_with_computer",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pct_homes_with_internet",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "socioeconomic_score",
                table: "ageb_populations",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avg_occupants_per_home",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "avg_schooling_years",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "estimated_socioeconomic_level",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "pct_homes_with_car",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "pct_homes_with_computer",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "pct_homes_with_internet",
                table: "ageb_populations");

            migrationBuilder.DropColumn(
                name: "socioeconomic_score",
                table: "ageb_populations");
        }
    }
}
