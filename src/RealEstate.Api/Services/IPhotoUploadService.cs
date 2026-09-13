namespace RealEstate.Api.Services;

public enum PhotoUploadErrorKind
{
    InvalidType,
    TooLarge,
    UploadFailed
}

public class PhotoUploadResult
{
    public List<string> Urls { get; init; } = new();
    public PhotoUploadErrorKind? ErrorKind { get; init; }
}

// Shared by AgentsController (one photo per call) and ListingsController (a batch of
// existing + new photos per call) so the allowed-type/size rules and S3 error handling
// can't drift between the two.
public interface IPhotoUploadService
{
    Task<PhotoUploadResult> UploadAsync(IFormFile photo, string keyPrefix);

    Task<PhotoUploadResult> UploadManyAsync(List<IFormFile> photos, string keyPrefix);
}
