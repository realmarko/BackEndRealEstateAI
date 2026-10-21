using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPrefersHighTrafficToBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "prefers_high_traffic",
                table: "business_type_criteria",
                type: "boolean",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 1,
                column: "prefers_high_traffic",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 2,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 3,
                column: "prefers_high_traffic",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 4,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 5,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 6,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 7,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 8,
                column: "prefers_high_traffic",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 9,
                column: "prefers_high_traffic",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10,
                column: "prefers_high_traffic",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 11,
                column: "prefers_high_traffic",
                value: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 12,
                column: "prefers_high_traffic",
                value: false);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 13,
                column: "prefers_high_traffic",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prefers_high_traffic",
                table: "business_type_criteria");
        }
    }
}
