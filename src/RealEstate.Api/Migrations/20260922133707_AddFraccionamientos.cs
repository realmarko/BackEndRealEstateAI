using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFraccionamientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fraccionamiento_id",
                table: "listings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fraccionamientos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    developer_name = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    stage = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    amenities_json = table.Column<string>(type: "text", nullable: true),
                    master_plan_image_url = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    first_detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    merged_into_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fraccionamientos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fraccionamiento_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fraccionamiento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    raw_data_json = table.Column<string>(type: "text", nullable: true),
                    detected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fraccionamiento_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_fraccionamiento_sources_fraccionamientos_fraccionamiento_id",
                        column: x => x.fraccionamiento_id,
                        principalTable: "fraccionamientos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_listings_fraccionamiento_id",
                table: "listings",
                column: "fraccionamiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraccionamiento_sources_fraccionamiento_id",
                table: "fraccionamiento_sources",
                column: "fraccionamiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraccionamientos_latitude_longitude",
                table: "fraccionamientos",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_fraccionamientos_status",
                table: "fraccionamientos",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_listings_fraccionamientos_fraccionamiento_id",
                table: "listings",
                column: "fraccionamiento_id",
                principalTable: "fraccionamientos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listings_fraccionamientos_fraccionamiento_id",
                table: "listings");

            migrationBuilder.DropTable(
                name: "fraccionamiento_sources");

            migrationBuilder.DropTable(
                name: "fraccionamientos");

            migrationBuilder.DropIndex(
                name: "ix_listings_fraccionamiento_id",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "fraccionamiento_id",
                table: "listings");
        }
    }
}
