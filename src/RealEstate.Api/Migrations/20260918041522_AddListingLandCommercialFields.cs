using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingLandCommercialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cadastral_value",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cos_coefficient",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cus_coefficient",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "frontage_depth_meters",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "frontage_width_meters",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_drainage",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_electricity",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_nearby_u_turn",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_potable_water",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_property_tax_debt",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_telecom_service",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_three_phase_electricity",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_vehicle_access",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_water_debt",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_corner_lot",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_flood_risk_zone",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_free_of_liens",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "land_tenure",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "land_use_zoning",
                table: "listings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lot_shape",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "max_height_meters",
                table: "listings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "primary_vialidad_type",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "street_frontage_count",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "topography",
                table: "listings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cadastral_value",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "cos_coefficient",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "cus_coefficient",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "frontage_depth_meters",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "frontage_width_meters",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_drainage",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_electricity",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_nearby_u_turn",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_potable_water",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_property_tax_debt",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_telecom_service",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_three_phase_electricity",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_vehicle_access",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_water_debt",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "is_corner_lot",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "is_flood_risk_zone",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "is_free_of_liens",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "land_tenure",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "land_use_zoning",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "lot_shape",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "max_height_meters",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "primary_vialidad_type",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "street_frontage_count",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "topography",
                table: "listings");
        }
    }
}
