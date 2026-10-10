using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IFactSheetPdfService
{
    byte[] Generate(ListingDto listing, IReadOnlyList<byte[]> photos);
}
