using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingPlazaPropertyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "commercial_unit_count",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_apartment_unit",
                table: "listings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commercial_unit_count",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "has_apartment_unit",
                table: "listings");
        }
    }
}
