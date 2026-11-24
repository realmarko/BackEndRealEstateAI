namespace RealEstate.Api.Models.Entities;

// An agent can belong to more than one brokerage at once (join table replacing the old
// single Agent.BrokerageId FK) — see AddAgentBrokeragesManyToMany migration.
public class AgentBrokerage
{
    public int AgentId { get; set; }
    public Agent Agent { get; set; } = null!;

    public int BrokerageId { get; set; }
    public Brokerage Brokerage { get; set; } = null!;

    // The brokerage shown wherever only one name fits (agent card, SEO structured data,
    // Agent.Company below) — exactly one row per agent should have this set; enforced in
    // AgentsController, not at the DB level.
    public bool IsPrimary { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
