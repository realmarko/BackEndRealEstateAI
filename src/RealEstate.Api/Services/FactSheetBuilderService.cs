using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

// Extracted from InquiriesController (its original, still-only caller) so
// ListingsController.ContactOwner can attach the same PDF to the real owner's contact email
// without duplicating the photo-download/agent-lookup/PDF-render pipeline.
public class FactSheetBuilderService : IFactSheetBuilderService
{
    private readonly ApplicationDbContext _db;
    private readonly IFactSheetPdfService _factSheetPdfService;
    private readonly IOwnerAgentLookupService _ownerAgentLookup;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FactSheetBuilderService> _logger;

    public FactSheetBuilderService(
        ApplicationDbContext db,
        IFactSheetPdfService factSheetPdfService,
        IOwnerAgentLookupService ownerAgentLookup,
        IHttpClientFactory httpClientFactory,
        ILogger<FactSheetBuilderService> logger)
    {
        _db = db;
        _factSheetPdfService = factSheetPdfService;
        _ownerAgentLookup = ownerAgentLookup;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<byte[]> BuildAsync(Listing listing)
    {
        // IsLoaded-guarded (not an unconditional LoadAsync) since callers differ in what they've
        // already Include()'d — InquiriesController.Create loads Address up front,
        // ListingsController.ContactOwner loads none of these — and reloading an already-loaded
        // nav property would be a wasted round-trip.
        var addressEntry = _db.Entry(listing).Reference(l => l.Address);
        if (!addressEntry.IsLoaded) await addressEntry.LoadAsync();

        var imagesEntry = _db.Entry(listing).Collection(l => l.Images);
        if (!imagesEntry.IsLoaded) await imagesEntry.LoadAsync();

        var ownerEntry = _db.Entry(listing).Reference(l => l.Owner);
        if (!ownerEntry.IsLoaded) await ownerEntry.LoadAsync();

        // Built from the same ListingDto shape the listing-detail page renders from, so the PDF
        // can never show different numbers than what the recipient already saw online. The
        // owner's real email/contact can go on it (see ListingExtensions.ToDto's isAuthenticated
        // redaction, which this bypasses on purpose) since every caller here only ever sends it
        // to someone who's already identified themselves (a qualifying lead) or to the listing's
        // own real owner.
        var dto = listing.ToDto(isAuthenticated: true);

        // Independent of each other (separate DbContext / HttpClient underneath), so run
        // concurrently instead of one after the other.
        var photosTask = DownloadPhotosAsync(dto.ImageUrls);
        var agentInfoTask = _ownerAgentLookup.GetAsync(listing.OwnerId);
        await Task.WhenAll(photosTask, agentInfoTask);

        var (company, photoUrl) = agentInfoTask.Result;
        dto.OwnerCompany = company;
        dto.OwnerPhotoUrl = photoUrl;

        // Only known after agentInfoTask resolves, so this download can't join the WhenAll above —
        // one extra best-effort fetch, same failure handling as the listing photos.
        var ownerPhoto = string.IsNullOrWhiteSpace(photoUrl)
            ? null
            : await DownloadPhotoAsync(_httpClientFactory.CreateClient("FactSheetPhotos"), photoUrl);

        return _factSheetPdfService.Generate(dto, photosTask.Result, ownerPhoto);
    }

    // Downloads up to FactSheetPdfService.MaxPhotos listing photos (S3 URLs, already in
    // ImageUrls' SortOrder) concurrently for the fact sheet's photo strip. Each download is
    // independently best-effort: one broken/slow image (a transient S3 hiccup, a malformed URL,
    // a photo deleted after the listing loaded) is logged and skipped rather than failing the
    // others or the email.
    private async Task<List<byte[]>> DownloadPhotosAsync(List<string> imageUrls)
    {
        var client = _httpClientFactory.CreateClient("FactSheetPhotos");
        var downloads = imageUrls.Take(FactSheetPdfService.MaxPhotos).Select(url => DownloadPhotoAsync(client, url));
        var results = await Task.WhenAll(downloads);
        return results.Where(bytes => bytes is not null).Select(bytes => bytes!).ToList();
    }

    private async Task<byte[]?> DownloadPhotoAsync(HttpClient client, string url)
    {
        try
        {
            return await client.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download listing photo {Url} for fact sheet", url);
            return null;
        }
    }
}
