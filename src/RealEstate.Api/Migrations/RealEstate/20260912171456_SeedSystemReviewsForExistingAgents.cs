using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class SeedSystemReviewsForExistingAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfills the same seeded 5-star "Agente Real Estate" review that AgentsController.Create
            // now adds automatically, for agents created before this feature existed.
            migrationBuilder.Sql(@"
                INSERT INTO agent_reviews (agent_id, reviewer_user_id, reviewer_name, rating, created_at)
                SELECT a.id, '00000000-0000-0000-0000-000000000000', 'Agente Real Estate', 5, now()
                FROM agents a
                WHERE NOT EXISTS (SELECT 1 FROM agent_reviews r WHERE r.agent_id = a.id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM agent_reviews
                WHERE reviewer_user_id = '00000000-0000-0000-0000-000000000000'
                  AND reviewer_name = 'Agente Real Estate';
            ");
        }
    }
}
