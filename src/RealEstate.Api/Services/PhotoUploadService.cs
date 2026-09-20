namespace RealEstate.Api.Services;

public class PhotoUploadService : IPhotoUploadService
{
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    private readonly IS3UploadService _s3Service;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ILogger<PhotoUploadService> _logger;

    public PhotoUploadService(IS3UploadService s3Service, IImageProcessingService imageProcessingService, ILogger<PhotoUploadService> logger)
    {
        _s3Service = s3Service;
        _imageProcessingService = imageProcessingService;
        _logger = logger;
    }

    public async Task<PhotoUploadResult> UploadAsync(IFormFile photo, string keyPrefix) =>
        await UploadManyAsync(new List<IFormFile> { photo }, keyPrefix);

    public async Task<PhotoUploadResult> UploadManyAsync(List<IFormFile> photos, string keyPrefix)
    {
        foreach (var photo in photos)
        {
            if (!AllowedPhotoTypes.Contains(photo.ContentType))
                return new PhotoUploadResult { ErrorKind = PhotoUploadErrorKind.InvalidType };
            if (photo.Length > MaxPhotoBytes)
                return new PhotoUploadResult { ErrorKind = PhotoUploadErrorKind.TooLarge };
        }

        // Tracked separately from the final result so that if upload N of M fails, the ones
        // that already succeeded (1..N-1) can be rolled back instead of left as billable,
        // unreferenced objects in the bucket.
        var uploadedUrls = new List<string>();
        try
        {
            foreach (var photo in photos)
            {
                // Resize + recompress before it ever reaches S3 — see ImageProcessingService.
                // A phone photo can be several MB at 4000x3000px; nobody views it larger than a
                // listing gallery image, and every visitor re-downloads whatever gets stored here.
                var processed = await _imageProcessingService.ProcessAsync(photo);
                using (processed.Content)
                {
                    uploadedUrls.Add(await _s3Service.UploadFileAsync(
                        processed.Content, processed.ContentType, processed.FileExtension, keyPrefix));
                }
            }
        }
        catch (ImageProcessingException ex)
        {
            _logger.LogError(ex, "Failed to process photo for prefix {KeyPrefix}", keyPrefix);
            await RollbackUploadsAsync(uploadedUrls);
            return new PhotoUploadResult { ErrorKind = PhotoUploadErrorKind.InvalidType };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload photo(s) to S3 for prefix {KeyPrefix}", keyPrefix);
            await RollbackUploadsAsync(uploadedUrls);
            return new PhotoUploadResult { ErrorKind = PhotoUploadErrorKind.UploadFailed };
        }

        return new PhotoUploadResult { Urls = uploadedUrls };
    }

    // Best-effort: a delete failure here shouldn't mask the original upload error, and an
    // orphaned object is a cheaper failure mode than losing the real error to a new exception.
    private async Task RollbackUploadsAsync(List<string> uploadedUrls)
    {
        foreach (var url in uploadedUrls)
        {
            try
            {
                await _s3Service.DeleteFileAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll back orphaned S3 object {Url} after a failed photo upload", url);
            }
        }
    }
}
