using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantListingLatLngIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_listings_latitude_longitude",
                table: "listings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_listings_latitude_longitude",
                table: "listings",
                columns: new[] { "latitude", "longitude" });
        }
    }
}
