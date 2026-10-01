using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class AddBrokerageProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "facebook_url",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instagram_url",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "brokerages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "brokerages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "description",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "facebook_url",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "instagram_url",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "state",
                table: "brokerages");

            migrationBuilder.DropColumn(
                name: "website",
                table: "brokerages");
        }
    }
}
