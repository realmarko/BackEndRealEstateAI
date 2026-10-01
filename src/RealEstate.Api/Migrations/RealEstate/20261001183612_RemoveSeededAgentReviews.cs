using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class RemoveSeededAgentReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AgentsController.Create no longer fabricates a 5-star "Agente Real Estate" review
            // for every new agent (see that controller) — this removes every review it (and the
            // SeedSystemReviewsForExistingAgents migration's backfill) already created, for both
            // real and test agents alike. Identical DELETE to that migration's own Down().
            migrationBuilder.Sql(@"
                DELETE FROM agent_reviews
                WHERE reviewer_user_id = '00000000-0000-0000-0000-000000000000'
                  AND reviewer_name = 'Agente Real Estate';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-seeds exactly one system review per agent that doesn't already have any reviews
            // — same shape as the original SeedSystemReviewsForExistingAgents.Up(), kept here so
            // this migration is properly reversible.
            migrationBuilder.Sql(@"
                INSERT INTO agent_reviews (agent_id, reviewer_user_id, reviewer_name, rating, created_at)
                SELECT a.id, '00000000-0000-0000-0000-000000000000', 'Agente Real Estate', 5, now()
                FROM agents a
                WHERE NOT EXISTS (SELECT 1 FROM agent_reviews r WHERE r.agent_id = a.id);
            ");
        }
    }
}
