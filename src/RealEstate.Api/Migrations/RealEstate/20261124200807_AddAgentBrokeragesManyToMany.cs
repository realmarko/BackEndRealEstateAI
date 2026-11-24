using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class AddAgentBrokeragesManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_brokerages",
                columns: table => new
                {
                    agent_id = table.Column<int>(type: "integer", nullable: false),
                    brokerage_id = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_brokerages", x => new { x.agent_id, x.brokerage_id });
                    table.ForeignKey(
                        name: "fk_agent_brokerages_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_brokerages_brokerages_brokerage_id",
                        column: x => x.brokerage_id,
                        principalTable: "brokerages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_brokerages_brokerage_id",
                table: "agent_brokerages",
                column: "brokerage_id");

            // Backfill: every agent that had a single brokerage_id becomes that agent's one
            // (primary) membership row, before the column it came from is dropped below.
            migrationBuilder.Sql(@"
                INSERT INTO agent_brokerages (agent_id, brokerage_id, is_primary, joined_at)
                SELECT id, brokerage_id, true, now()
                FROM agents
                WHERE brokerage_id IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "fk_agents_brokerages_brokerage_id",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "ix_agents_brokerage_id",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "brokerage_id",
                table: "agents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "brokerage_id",
                table: "agents",
                type: "integer",
                nullable: true);

            // Best-effort reverse backfill: each agent's primary membership (or, failing that,
            // its oldest one) becomes the single brokerage_id again — any additional
            // memberships beyond the first are lost, same as any other migration rollback that
            // undoes a real schema widening.
            migrationBuilder.Sql(@"
                UPDATE agents a
                SET brokerage_id = sub.brokerage_id
                FROM (
                    SELECT DISTINCT ON (agent_id) agent_id, brokerage_id
                    FROM agent_brokerages
                    ORDER BY agent_id, is_primary DESC, joined_at ASC
                ) sub
                WHERE a.id = sub.agent_id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_agents_brokerage_id",
                table: "agents",
                column: "brokerage_id");

            migrationBuilder.AddForeignKey(
                name: "fk_agents_brokerages_brokerage_id",
                table: "agents",
                column: "brokerage_id",
                principalTable: "brokerages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.DropTable(
                name: "agent_brokerages");
        }
    }
}
