using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddListingBrokerageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "brokerage_id",
                table: "listings",
                type: "integer",
                nullable: true);

            // Backfill from whichever source happens to still exist when this runs — this
            // migration lives in a separate DbContext/migration history from
            // AddAgentBrokeragesManyToMany (RealEstateDbContext), so deployment order between
            // the two isn't guaranteed. If that migration already ran, agent_brokerages exists
            // and agents.brokerage_id is gone; if it hasn't yet, it's the other way around.
            // Either way, every existing listing is backfilled to the brokerage its owning agent
            // had at the time — same source data, just read from wherever it currently lives.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'agent_brokerages') THEN
                        UPDATE listings l
                        SET brokerage_id = sub.brokerage_id
                        FROM (
                            SELECT a.user_id, ab.brokerage_id
                            FROM agents a
                            JOIN agent_brokerages ab ON ab.agent_id = a.id AND ab.is_primary = true
                            WHERE a.user_id IS NOT NULL
                        ) sub
                        WHERE l.owner_id = sub.user_id;
                    ELSIF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'agents' AND column_name = 'brokerage_id') THEN
                        UPDATE listings l
                        SET brokerage_id = a.brokerage_id
                        FROM agents a
                        WHERE l.owner_id = a.user_id AND a.brokerage_id IS NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "brokerage_id",
                table: "listings");
        }
    }
}
