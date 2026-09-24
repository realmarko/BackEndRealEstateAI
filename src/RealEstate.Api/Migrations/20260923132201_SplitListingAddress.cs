using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitListingAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listing_addresses",
                columns: table => new
                {
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    colonia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    zip_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_addresses", x => x.listing_id);
                    table.ForeignKey(
                        name: "fk_listing_addresses_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listing_addresses_city",
                table: "listing_addresses",
                column: "city");

            // Backfill from the columns being dropped below — colonia didn't exist as a separate
            // concept before this migration, so it's left blank for an agent to fill in when they
            // next edit the listing (no plausible value to guess it from address_line). country
            // defaults to 'México': every listing in this app is Mexican real estate today.
            migrationBuilder.Sql(@"
                INSERT INTO listing_addresses (listing_id, street, colonia, city, state, zip_code, country)
                SELECT id, address_line, '', city, state, COALESCE(zip_code, ''), 'México'
                FROM listings;
            ");

            migrationBuilder.DropIndex(
                name: "ix_listings_city",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "address_line",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "city",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "state",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "zip_code",
                table: "listings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_line",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "zip_code",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Colonia and country have no home in the pre-split schema, so they're dropped here —
            // Down() restores the old shape, not every value that only made sense in the new one.
            migrationBuilder.Sql(@"
                UPDATE listings l
                SET address_line = a.street, city = a.city, state = a.state, zip_code = a.zip_code
                FROM listing_addresses a
                WHERE a.listing_id = l.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_listings_city",
                table: "listings",
                column: "city");

            migrationBuilder.DropTable(
                name: "listing_addresses");
        }
    }
}
