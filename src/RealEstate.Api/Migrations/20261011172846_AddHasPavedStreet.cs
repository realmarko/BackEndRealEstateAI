using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHasPavedStreet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_paved_street",
                table: "listings",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_paved_street",
                table: "listings");
        }
    }
}
