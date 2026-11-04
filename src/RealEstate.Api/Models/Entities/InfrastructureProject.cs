namespace RealEstate.Api.Models.Entities;

public enum InfrastructureProjectType
{
    HighwayExpansion = 0,
    NewRoad = 1,
    IndustrialPark = 2,
    PublicTransit = 3,
    Hospital = 4,
    School = 5,
    Other = 6
}

public enum InfrastructureProjectStatus
{
    Announced = 0,
    InProgress = 1,
    Completed = 2,
    // Soft-retire, same reasoning as FraccionamientoStatus.Rejected/ErrorLog never hard-deleting
    // a row — an admin who mis-captured or later learns a project fell through marks it Cancelled
    // instead of removing the record outright.
    Cancelled = 3
}

// A publicly announced infrastructure project (highway expansion, new industrial park, transit
// line, hospital, school) captured manually by an admin — there's no structured/scrapeable feed
// for this kind of announcement (government press releases, news), so this is admin-entered data,
// not detected like Fraccionamiento. Used as a "potencial de plusvalía" signal
// (AppreciationPotentialService) and shown in the commercial viability report
// (ViabilityAnalysisService) via the shared InfrastructureProjectProximity helper.
public class InfrastructureProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public InfrastructureProjectType Type { get; set; }
    public InfrastructureProjectStatus Status { get; set; } = InfrastructureProjectStatus.Announced;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    // How far this project's effect on appreciation/viability realistically reaches — defaults to
    // 2000m (same real-world distance ViabilityAnalysisService/AppreciationPotentialService
    // already use for "a new development's demand reaches this far") when the admin doesn't
    // override it for a project with an unusually large or small footprint.
    public double AffectedRadiusMeters { get; set; } = 2000;

    public DateTime AnnouncedDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    // Link to the news/government announcement this was captured from — lets a viewer verify the
    // claim themselves rather than taking the platform's word for it.
    public string? SourceUrl { get; set; }

    // No navigation property on purpose, same reasoning as Fraccionamiento.ReviewedByUserId — an
    // audit trail, not a live relationship that should block or cascade off the account.
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
