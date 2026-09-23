namespace RealEstate.Api.Models.Entities;

// The 32 official INEGI entidades federativas — static reference data (doesn't change), seeded
// once by its own migration. Separate from MunicipalBoundary: that table only has real polygon
// data imported for Puebla so far, but every state's name is real, known, unchanging data the
// listing form's State field can offer regardless of which states have municipio/city data yet.
public class MexicanState
{
    public string Code { get; set; } = string.Empty; // 2-digit INEGI entidad code, e.g. "21" for Puebla
    public string Name { get; set; } = string.Empty;
}
