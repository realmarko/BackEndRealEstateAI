using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public class S3Options
{
    public string BucketName { get; set; } = string.Empty;
}

public class S3UploadService : IS3UploadService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public S3UploadService(IAmazonS3 s3Client, IOptions<S3Options> options)
    {
        _s3Client = s3Client;
        _bucketName = options.Value.BucketName;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string keyPrefix)
    {
        using var stream = file.OpenReadStream();
        return await UploadFileAsync(stream, file.ContentType, Path.GetExtension(file.FileName), keyPrefix);
    }

    public async Task<string> UploadFileAsync(Stream content, string contentType, string fileExtension, string keyPrefix)
    {
        var key = $"{keyPrefix}/{Guid.NewGuid()}{fileExtension}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType
        };
        // Every key is a brand-new GUID (never reused, never overwritten — see the delete+reupload
        // pattern callers use for "replacing" a photo), so the object itself is truly immutable:
        // safe for the browser/CDN to cache forever instead of re-downloading it on every view.
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await _s3Client.PutObjectAsync(request);

        var region = _s3Client.Config.RegionEndpoint?.SystemName ?? "us-east-1";
        return $"https://{_bucketName}.s3.{region}.amazonaws.com/{key}";
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        var key = new Uri(fileUrl).AbsolutePath.TrimStart('/');
        await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        });
    }
}
