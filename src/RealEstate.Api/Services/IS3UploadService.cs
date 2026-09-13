namespace RealEstate.Api.Services;

public interface IS3UploadService
{
    Task<string> UploadFileAsync(IFormFile file, string keyPrefix);

    // Best-effort cleanup for an object this service previously uploaded (e.g. rolling back
    // a partially-succeeded multi-file upload). fileUrl is the URL UploadFileAsync returned.
    Task DeleteFileAsync(string fileUrl);
}
