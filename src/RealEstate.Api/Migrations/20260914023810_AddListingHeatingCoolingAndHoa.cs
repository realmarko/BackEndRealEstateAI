using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingHeatingCoolingAndHoa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_heating_cooling",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "hoa_fee",
                table: "listings",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_heating_cooling",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "hoa_fee",
                table: "listings");
        }
    }
}
