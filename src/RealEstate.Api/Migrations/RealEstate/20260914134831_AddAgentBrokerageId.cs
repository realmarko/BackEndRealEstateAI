using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class AddAgentBrokerageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "brokerage_id",
                table: "agents",
                type: "integer",
                nullable: true);

            // The plain unique index on brokerages.name (from AddBrokerageCatalog) is
            // case-sensitive, but every app-level lookup (AgentsController.ResolveOrCreateBrokerageAsync)
            // treats names as case-insensitive — without this, two concurrent requests for
            // "Century 21" and "century 21" could both pass the pre-check and both insert,
            // silently forking the catalog instead of hitting the unique-index race guard.
            migrationBuilder.DropIndex(
                name: "ix_brokerages_name",
                table: "brokerages");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ix_brokerages_name ON brokerages (LOWER(name));
            ");

            // Backfill: create any brokerage row that only ever existed as free text on an
            // agent (shouldn't normally happen, since AgentsController.ResolveOrCreateBrokerageAsync
            // always upserts the catalog first — this is a safety net), matching case-insensitively
            // and ignoring blank/whitespace-only values (same TRIM guard as AddBrokerageCatalog's
            // own backfill), so "Century 21" and "century 21" collapse onto one row instead of
            // forking the catalog.
            migrationBuilder.Sql(@"
                INSERT INTO brokerages (name)
                SELECT sub.name FROM (
                    SELECT DISTINCT ON (LOWER(TRIM(company))) TRIM(company) AS name
                    FROM agents
                    WHERE company IS NOT NULL AND TRIM(company) <> ''
                    ORDER BY LOWER(TRIM(company)), TRIM(company)
                ) sub
                WHERE NOT EXISTS (
                    SELECT 1 FROM brokerages b WHERE LOWER(b.name) = LOWER(sub.name)
                );

                -- A scalar subquery (not UPDATE ... FROM) so a match is always exactly one row,
                -- even if the pre-existing catalog somehow already held a case-duplicate pair.
                UPDATE agents a
                SET brokerage_id = (
                    SELECT b.id FROM brokerages b
                    WHERE LOWER(b.name) = LOWER(TRIM(a.company))
                    ORDER BY b.id
                    LIMIT 1
                )
                WHERE a.company IS NOT NULL AND TRIM(a.company) <> '';
            ");

            // Raw Sql() calls give no affected-row feedback, so this is the only thing standing
            // between a silent partial backfill (e.g. some unforeseen edge case in the matching
            // above) and DropColumn below permanently destroying that data with no way to recover it.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    unmatched_count integer;
                BEGIN
                    SELECT count(*) INTO unmatched_count
                    FROM agents
                    WHERE company IS NOT NULL AND TRIM(company) <> '' AND brokerage_id IS NULL;

                    IF unmatched_count > 0 THEN
                        RAISE EXCEPTION
                            'AddAgentBrokerageId: % agent(s) have a company value that failed to backfill to brokerage_id',
                            unmatched_count;
                    END IF;
                END $$;
            ");

            migrationBuilder.DropColumn(
                name: "company",
                table: "agents");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agents_brokerages_brokerage_id",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "ix_agents_brokerage_id",
                table: "agents");

            migrationBuilder.AddColumn<string>(
                name: "company",
                table: "agents",
                type: "text",
                nullable: true);

            // Best-effort only: if Up() ever collapsed two differently-cased agent.company
            // values onto one brokerage row, both agents get that row's canonical casing back
            // here, not their own original text — an unavoidable, one-way loss from the collapse
            // itself, not from this restore step.
            migrationBuilder.Sql(@"
                UPDATE agents a
                SET company = b.name
                FROM brokerages b
                WHERE a.brokerage_id = b.id;
            ");

            migrationBuilder.DropColumn(
                name: "brokerage_id",
                table: "agents");

            migrationBuilder.Sql(@"
                DROP INDEX ix_brokerages_name;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_brokerages_name",
                table: "brokerages",
                column: "name",
                unique: true);
        }
    }
}
