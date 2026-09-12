using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class AddBrokerageCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brokerages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brokerages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brokerages_name",
                table: "brokerages",
                column: "name",
                unique: true);

            // Backfill the catalog from company names already captured on existing agents.
            migrationBuilder.Sql(@"
                INSERT INTO brokerages (name)
                SELECT DISTINCT ON (LOWER(company)) company
                FROM agents
                WHERE company IS NOT NULL AND TRIM(company) <> ''
                ORDER BY LOWER(company), company;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brokerages");
        }
    }
}
