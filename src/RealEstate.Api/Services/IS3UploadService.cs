namespace RealEstate.Api.Services;

public interface IS3UploadService
{
    Task<string> UploadFileAsync(IFormFile file, string keyPrefix);

    // For content that isn't (or is no longer) an IFormFile — e.g. PhotoUploadService uploading
    // an already-processed/re-encoded image. Callers own disposing `content`.
    Task<string> UploadFileAsync(Stream content, string contentType, string fileExtension, string keyPrefix);

    // Best-effort cleanup for an object this service previously uploaded (e.g. rolling back
    // a partially-succeeded multi-file upload). fileUrl is the URL UploadFileAsync returned.
    Task DeleteFileAsync(string fileUrl);
}
