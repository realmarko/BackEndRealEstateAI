namespace RealEstate.Api.Models.Entities;

// Fixed catalog of general land-use classifications ("uso de suelo") a Land listing can be
// tagged with — Urbano, Urbanizable, No urbanizable, Industrial, Residencial, Comercial,
// Agrícola. Seeded once via migration (see AddLandUseCategoryCatalog); not user-editable, so
// there's no create/update endpoint for it, just the read-only list.
public class LandUseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
