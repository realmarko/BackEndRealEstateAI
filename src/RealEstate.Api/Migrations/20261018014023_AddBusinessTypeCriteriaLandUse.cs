using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTypeCriteriaLandUse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "acceptable_land_use_categories",
                table: "business_type_criteria",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 1,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 2,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 3,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 4,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 5,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 6,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 7,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 8,
                column: "acceptable_land_use_categories",
                value: "Urbano,Comercial,Residencial");

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 9,
                column: "acceptable_land_use_categories",
                value: "Industrial,Urbano");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "acceptable_land_use_categories",
                table: "business_type_criteria");
        }
    }
}
