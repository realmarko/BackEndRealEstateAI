namespace RealEstate.Api.Models.Entities;

// One row per site navigation (frontend's App component fires this on every Angular
// NavigationEnd — see app.ts) — sitewide traffic/analytics, distinct from Listing.ViewCount/
// Agent.ViewCount (a per-entity, 24h-deduplicated lifetime counter). This is deliberately NOT
// deduplicated: standard "page view" semantics count every navigation, not unique visits.
// Admin-only to read (PageViewsController.Stats) — never surfaced anywhere public.
public class PageView
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The Angular route's own pattern (e.g. "listings/:id", "map", "" for home) — what the
    // weekly stats group by, so every individual listing/agent/etc. page rolls up into one
    // "Detalle de propiedad"-style row instead of one row per real-world entity.
    public string PathPattern { get; set; } = string.Empty;

    // The actual URL navigated to (e.g. "/listings/abc-123") — kept for context/debugging only,
    // never grouped/aggregated on.
    public string Path { get; set; } = string.Empty;

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
