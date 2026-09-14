using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingParkingFloorsLotGarden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "floors",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "garden_size_sqm",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "lot_size_sqm",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parking_spaces",
                table: "listings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "floors",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "garden_size_sqm",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "lot_size_sqm",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "parking_spaces",
                table: "listings");
        }
    }
}
