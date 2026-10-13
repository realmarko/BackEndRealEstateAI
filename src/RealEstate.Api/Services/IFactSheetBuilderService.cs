using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IFactSheetBuilderService
{
    /// <summary>Generates the fact-sheet PDF (specs, address, photos, contact) for a listing —
    /// the same shared artifact InquiriesController emails to a qualifying lead and
    /// ListingsController.ContactOwner attaches for the real property owner.</summary>
    Task<byte[]> BuildAsync(Listing listing);
}
