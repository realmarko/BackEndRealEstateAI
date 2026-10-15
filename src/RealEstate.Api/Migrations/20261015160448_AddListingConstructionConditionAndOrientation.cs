using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingConstructionConditionAndOrientation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "construction_condition",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "facade_orientation",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_lp_gas",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_natural_gas",
                table: "listings",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "parking_is_covered",
                table: "listings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "construction_condition",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "facade_orientation",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_lp_gas",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_natural_gas",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "parking_is_covered",
                table: "listings");
        }
    }
}
