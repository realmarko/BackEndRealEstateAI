namespace RealEstate.Api.Models.Entities;

// A growing catalog of real estate agencies, built up as agents register their company name.
// Powers the autocomplete suggestions on the agent signup form.
public class Brokerage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
