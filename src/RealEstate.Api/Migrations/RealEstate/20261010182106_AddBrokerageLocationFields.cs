using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class AddBrokerageLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "brokerages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "brokerages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "working_hours",
                table: "brokerages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "email",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "working_hours",
                table: "brokerages");
        }
    }
}
