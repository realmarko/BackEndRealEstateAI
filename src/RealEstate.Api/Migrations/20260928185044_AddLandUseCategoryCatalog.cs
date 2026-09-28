using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLandUseCategoryCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "land_use_category_id",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "land_use_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_land_use_categories", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "land_use_categories",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Urbano" },
                    { 2, "Urbanizable" },
                    { 3, "No urbanizable" },
                    { 4, "Industrial" },
                    { 5, "Residencial" },
                    { 6, "Comercial" },
                    { 7, "Agrícola" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_listings_land_use_category_id",
                table: "listings",
                column: "land_use_category_id");

            migrationBuilder.AddForeignKey(
                name: "fk_listings_land_use_categories_land_use_category_id",
                table: "listings",
                column: "land_use_category_id",
                principalTable: "land_use_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listings_land_use_categories_land_use_category_id",
                table: "listings");

            migrationBuilder.DropTable(
                name: "land_use_categories");

            migrationBuilder.DropIndex(
                name: "ix_listings_land_use_category_id",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "land_use_category_id",
                table: "listings");
        }
    }
}
