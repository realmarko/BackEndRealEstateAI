using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBenefitsFromNearbyResidentialGrowth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "benefits_from_nearby_residential_growth",
                table: "business_type_criteria",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 1,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 2,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 3,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 4,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 5,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 6,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 7,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 8,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 9,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 11,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 12,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 13,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 14,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 15,
                column: "benefits_from_nearby_residential_growth",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 16,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 17,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 18,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 19,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 20,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 21,
                column: "benefits_from_nearby_residential_growth",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 22,
                column: "benefits_from_nearby_residential_growth",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "benefits_from_nearby_residential_growth",
                table: "business_type_criteria");
        }
    }
}
