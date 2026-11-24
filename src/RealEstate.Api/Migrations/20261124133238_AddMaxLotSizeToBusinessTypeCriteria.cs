using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxLotSizeToBusinessTypeCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "max_lot_size_sqm",
                table: "business_type_criteria",
                type: "double precision",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 1,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 2,
                column: "max_lot_size_sqm",
                value: 2000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 3,
                column: "max_lot_size_sqm",
                value: 2000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 4,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 5,
                column: "max_lot_size_sqm",
                value: 2000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 6,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 7,
                column: "max_lot_size_sqm",
                value: 1000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 8,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 9,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 10,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 11,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 12,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 13,
                column: "max_lot_size_sqm",
                value: 3000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 14,
                column: "max_lot_size_sqm",
                value: 180.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 15,
                column: "max_lot_size_sqm",
                value: null);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 16,
                column: "max_lot_size_sqm",
                value: 500.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 17,
                column: "max_lot_size_sqm",
                value: 400.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 18,
                column: "max_lot_size_sqm",
                value: 400.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 19,
                column: "max_lot_size_sqm",
                value: 350.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 20,
                column: "max_lot_size_sqm",
                value: 1000.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 21,
                column: "max_lot_size_sqm",
                value: 300.0);

            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 22,
                column: "max_lot_size_sqm",
                value: 400.0);

            // Id 23 (pizzeria) was seeded by a later migration (AddPizzeriaBusinessTypeCriteria),
            // not by the HasData block the scaffolder diffed above — added by hand so a fresh
            // database gets the same value this migration also applies directly to dev/prod.
            migrationBuilder.UpdateData(
                table: "business_type_criteria",
                keyColumn: "id",
                keyValue: 23,
                column: "max_lot_size_sqm",
                value: 600.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_lot_size_sqm",
                table: "business_type_criteria");
        }
    }
}
