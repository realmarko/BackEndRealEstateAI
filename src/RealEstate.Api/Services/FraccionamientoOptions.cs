namespace RealEstate.Api.Services;

// The shared secret n8n sends in the X-Ingestion-Key header on POST /api/fraccionamientos/candidates
// — that endpoint has no human user behind it, so it can't use the normal JWT auth. Generate a
// long random value and set it via dotnet user-secrets locally / EB environment variables in
// deployed environments, same as every other real secret in this app.
public class FraccionamientoOptions
{
    public string IngestionApiKey { get; set; } = "CHANGE_ME_FRACCIONAMIENTOS_INGESTION_KEY";
}
