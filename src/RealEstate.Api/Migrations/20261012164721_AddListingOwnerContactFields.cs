using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingOwnerContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hide_agent_contact",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "owner_contact_email",
                table: "listings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_contact_name",
                table: "listings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_contact_phone",
                table: "listings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hide_agent_contact",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "owner_contact_email",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "owner_contact_name",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "owner_contact_phone",
                table: "listings");
        }
    }
}
