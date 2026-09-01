namespace RealEstate.Api.Services;

public interface IS3UploadService
{
    Task<string> UploadFileAsync(IFormFile file, string keyPrefix);
}
