using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingSearchPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_listing_addresses_city",
                table: "listing_addresses");

            migrationBuilder.AddColumn<Point>(
                name: "location",
                table: "listings",
                type: "geometry(Point,4326)",
                nullable: true,
                computedColumnSql: "ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "city_lower",
                table: "listing_addresses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                computedColumnSql: "lower(city)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_listings_location",
                table: "listings",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_listings_price",
                table: "listings",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_listings_status_listing_type_property_type_created_at",
                table: "listings",
                columns: new[] { "status", "listing_type", "property_type", "created_at" },
                filter: "status <> 3");

            migrationBuilder.CreateIndex(
                name: "ix_listing_addresses_city_lower",
                table: "listing_addresses",
                column: "city_lower");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_listings_location",
                table: "listings");

            migrationBuilder.DropIndex(
                name: "ix_listings_price",
                table: "listings");

            migrationBuilder.DropIndex(
                name: "ix_listings_status_listing_type_property_type_created_at",
                table: "listings");

            migrationBuilder.DropIndex(
                name: "ix_listing_addresses_city_lower",
                table: "listing_addresses");

            migrationBuilder.DropColumn(
                name: "location",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "city_lower",
                table: "listing_addresses");

            migrationBuilder.CreateIndex(
                name: "ix_listing_addresses_city",
                table: "listing_addresses",
                column: "city");
        }
    }
}
