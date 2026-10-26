using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFraccionamientoAddressRatingWebsite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "fraccionamientos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "google_rating",
                table: "fraccionamientos",
                type: "numeric(2,1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website_url",
                table: "fraccionamientos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "fraccionamientos");

            migrationBuilder.DropColumn(
                name: "google_rating",
                table: "fraccionamientos");

            migrationBuilder.DropColumn(
                name: "website_url",
                table: "fraccionamientos");
        }
    }
}
