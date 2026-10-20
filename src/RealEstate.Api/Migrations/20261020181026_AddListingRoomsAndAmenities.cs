using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingRoomsAndAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_balcony",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_bbq_area",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_dining_room",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_elevator",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_gym",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_kitchen",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_laundry_area",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_living_room",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_pool",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_security",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_service_room",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_storage_room",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_terrace",
                table: "listings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_balcony",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_bbq_area",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_dining_room",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_elevator",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_gym",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_kitchen",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_laundry_area",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_living_room",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_pool",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_security",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_service_room",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_storage_room",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_terrace",
                table: "listings");
        }
    }
}
