namespace RealEstate.Api.Services;

// Config for the dev-only "Enviar a producción" admin feature (ProdSyncService.SendToProdAsync,
// ListingsController's prod-sync/* endpoints). TargetApiUrl is only meaningfully set in DEV's
// secrets (the absolute URL of prod's API) — prod itself never calls out, it only validates
// SharedSecret on incoming requests, so TargetApiUrl stays empty there and SendToProdAsync treats
// that as "not configured in this environment". SharedSecret must be the exact same value in
// both environments' user-secrets: it stands in for a user JWT on these two endpoints because
// dev and prod are separate Identity databases — a dev admin's JWT proves nothing on prod.
public class ProdSyncOptions
{
    public string? TargetApiUrl { get; set; }
    public string SharedSecret { get; set; } = "CHANGE_ME_PROD_SYNC_SHARED_SECRET";
}
