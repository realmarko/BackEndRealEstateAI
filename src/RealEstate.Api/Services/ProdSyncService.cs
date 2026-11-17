using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealEstate.Api.Data;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

// Create-only result: AlreadyExists is never an error (the admin just asked to send something
// that's already there — see ListingsController's "never overwrite" behavior), ErrorMessage is
// null in that case too.
public record ProdSyncResult(bool Success, bool AlreadyExists, string? ErrorMessage);

public interface IProdSyncService
{
    Task<ProdSyncResult> SendToProdAsync(Guid listingId, CancellationToken cancellationToken = default);
}

// Backs ListingsController.SendToProd ("Enviar a producción", admin-only, dev-only). Downloads
// the listing's own photos from this environment's S3 bucket (their URLs are already public —
// the same ones rendered in <img> tags on the live site) and forwards everything, including the
// photo bytes, as one multipart request to prod's ProdSyncImport endpoint — reusing prod's own
// Create()-style photo pipeline there instead of copying S3 objects directly, so photos get the
// exact same validation/resize treatment a normal upload would get.
public class ProdSyncService : IProdSyncService
{
    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ProdSyncOptions _options;
    private readonly ILogger<ProdSyncService> _logger;

    public ProdSyncService(HttpClient http, ApplicationDbContext db, IOptions<ProdSyncOptions> options, ILogger<ProdSyncService> logger)
    {
        _http = http;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProdSyncResult> SendToProdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        var targetUrl = _options.TargetApiUrl;
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return new ProdSyncResult(false, false, "ProdSync no está configurado en este entorno (falta ProdSync:TargetApiUrl).");
        }

        var listing = await _db.Listings
            .Include(l => l.Address)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);
        if (listing is null || listing.Address is null)
        {
            return new ProdSyncResult(false, false, "Listing not found.");
        }

        var alreadyExists = await CheckExistsOnProdAsync(targetUrl, listingId, cancellationToken);
        if (alreadyExists is null)
        {
            return new ProdSyncResult(false, false, "No se pudo verificar si ya existe en producción. Intenta de nuevo.");
        }
        if (alreadyExists.Value)
        {
            return new ProdSyncResult(false, true, null);
        }

        var owner = await _db.Users.FirstOrDefaultAsync(u => u.Id == listing.OwnerId, cancellationToken);

        using var content = new MultipartFormDataContent();
        AddFields(content, listing);
        content.Add(new StringContent(owner?.Email ?? string.Empty), "OwnerEmail");

        try
        {
            foreach (var image in listing.Images.OrderBy(i => i.SortOrder))
            {
                var bytes = await _http.GetByteArrayAsync(image.Url, cancellationToken);
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = GuessContentType(image.Url);
                content.Add(fileContent, "Photos", Path.GetFileName(new Uri(image.Url).LocalPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProdSync: failed to download a photo for listing {ListingId} from this environment's bucket", listingId);
            return new ProdSyncResult(false, false, "No se pudieron descargar las fotos de esta propiedad. Intenta de nuevo.");
        }

        using var importRequest = new HttpRequestMessage(HttpMethod.Post, $"{targetUrl}/listings/prod-sync/import") { Content = content };
        importRequest.Headers.Add("X-Prod-Sync-Secret", _options.SharedSecret);

        try
        {
            using var importResponse = await _http.SendAsync(importRequest, cancellationToken);

            if (importResponse.StatusCode == HttpStatusCode.Conflict)
            {
                return new ProdSyncResult(false, true, null);
            }

            if (!importResponse.IsSuccessStatusCode)
            {
                var body = await importResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("ProdSync import failed for listing {ListingId}: {Status} {Body}", listingId, importResponse.StatusCode, body);
                return new ProdSyncResult(false, false, "Producción rechazó el envío. Intenta de nuevo o revisa los logs.");
            }

            return new ProdSyncResult(true, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProdSync: request to prod failed for listing {ListingId}", listingId);
            return new ProdSyncResult(false, false, "No se pudo contactar a producción. Intenta de nuevo.");
        }
    }

    // null means "couldn't tell" (network/auth failure) — distinct from a confident false, so the
    // caller never proceeds to a full photo transfer on the strength of an unanswered check.
    private async Task<bool?> CheckExistsOnProdAsync(string targetUrl, Guid listingId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{targetUrl}/listings/prod-sync/{listingId}/exists");
        request.Headers.Add("X-Prod-Sync-Secret", _options.SharedSecret);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("exists", out var existsEl) && existsEl.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProdSync: exists-check request to prod failed for listing {ListingId}", listingId);
            return null;
        }
    }

    // Matches ListingsController.ProdSyncImport's ProdSyncImportDto property names exactly — the
    // receiving side binds these via [FromForm], the same way a browser's multipart upload binds
    // to ListingCreateDto/ListingUpdateDto today. Mirrors every field Create() reads off that DTO.
    private static void AddFields(MultipartFormDataContent content, Listing listing)
    {
        var address = listing.Address!;

        void Add(string name, object? value)
        {
            if (value is null) return;
            var str = value switch
            {
                bool b => b.ToString(),
                Enum e => Convert.ToInt32(e).ToString(CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            if (str is not null) content.Add(new StringContent(str), name);
        }

        Add("Id", listing.Id);
        Add("Title", listing.Title);
        Add("Description", listing.Description);
        Add("ListingType", listing.ListingType);
        Add("PropertyType", listing.PropertyType);
        Add("Status", listing.Status);
        Add("Price", listing.Price);
        Add("PriceOnRequest", listing.PriceOnRequest);
        Add("Currency", listing.Currency);
        Add("Street", address.Street);
        Add("Colonia", address.Colonia);
        Add("City", address.City);
        Add("State", address.State);
        Add("ZipCode", address.ZipCode);
        Add("Country", address.Country);
        Add("Latitude", listing.Latitude);
        Add("Longitude", listing.Longitude);
        Add("Bedrooms", listing.Bedrooms);
        Add("Bathrooms", listing.Bathrooms);
        Add("AreaSqFt", listing.AreaSqFt);
        Add("YearBuilt", listing.YearBuilt);
        Add("ParkingSpaces", listing.ParkingSpaces);
        Add("ParkingIsCovered", listing.ParkingIsCovered);
        Add("Floors", listing.Floors);
        Add("LotSizeSqm", listing.LotSizeSqm);
        Add("GardenSizeSqm", listing.GardenSizeSqm);
        Add("HasHeatingCooling", listing.HasHeatingCooling);
        Add("HasRoofGarden", listing.HasRoofGarden);
        Add("HoaFee", listing.HoaFee);
        Add("VideoTourUrl", listing.VideoTourUrl);
        Add("ConstructionCondition", listing.ConstructionCondition);
        Add("FacadeOrientation", listing.FacadeOrientation);
        Add("HasNaturalGas", listing.HasNaturalGas);
        Add("HasLpGas", listing.HasLpGas);
        Add("HasCistern", listing.HasCistern);
        Add("CisternCapacityLiters", listing.CisternCapacityLiters);
        Add("HasWell", listing.HasWell);
        Add("HasKitchen", listing.HasKitchen);
        Add("HasLivingRoom", listing.HasLivingRoom);
        Add("HasDiningRoom", listing.HasDiningRoom);
        Add("HasPool", listing.HasPool);
        Add("HasTerrace", listing.HasTerrace);
        Add("HasBalcony", listing.HasBalcony);
        Add("HasServiceRoom", listing.HasServiceRoom);
        Add("HasLaundryArea", listing.HasLaundryArea);
        Add("HasStorageRoom", listing.HasStorageRoom);
        Add("HasGym", listing.HasGym);
        Add("HasSecurity", listing.HasSecurity);
        Add("HasElevator", listing.HasElevator);
        Add("HasBbqArea", listing.HasBbqArea);
        Add("LandUseZoning", listing.LandUseZoning);
        Add("LandUseCategoryId", listing.LandUseCategoryId);
        Add("LandTenure", listing.LandTenure);
        Add("HasEjidalCertificate", listing.HasEjidalCertificate);
        Add("CosCoefficient", listing.CosCoefficient);
        Add("CusCoefficient", listing.CusCoefficient);
        Add("MaxHeightMeters", listing.MaxHeightMeters);
        Add("IsFreeOfLiens", listing.IsFreeOfLiens);
        Add("HasPropertyTaxDebt", listing.HasPropertyTaxDebt);
        Add("HasWaterDebt", listing.HasWaterDebt);
        Add("FrontageWidthMeters", listing.FrontageWidthMeters);
        Add("FrontageDepthMeters", listing.FrontageDepthMeters);
        Add("CommercialUnitCount", listing.CommercialUnitCount);
        Add("HasApartmentUnit", listing.HasApartmentUnit);
        Add("HasPotableWater", listing.HasPotableWater);
        Add("HasDrainage", listing.HasDrainage);
        Add("HasElectricity", listing.HasElectricity);
        Add("HasThreePhaseElectricity", listing.HasThreePhaseElectricity);
        Add("HasTelecomService", listing.HasTelecomService);
        Add("HasVehicleAccess", listing.HasVehicleAccess);
        Add("HasPavedStreet", listing.HasPavedStreet);
        Add("HasNearbyUTurn", listing.HasNearbyUTurn);
        Add("IsCornerLot", listing.IsCornerLot);
        Add("StreetFrontageCount", listing.StreetFrontageCount);
        Add("PrimaryVialidadType", listing.PrimaryVialidadType);
        Add("LotShape", listing.LotShape);
        Add("Topography", listing.Topography);
        Add("IsFloodRiskZone", listing.IsFloodRiskZone);
        Add("CadastralValue", listing.CadastralValue);
    }

    private static MediaTypeHeaderValue GuessContentType(string url)
    {
        var extension = Path.GetExtension(new Uri(url).LocalPath).ToLowerInvariant();
        var mime = extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return MediaTypeHeaderValue.Parse(mime);
    }
}
