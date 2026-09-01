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
        var key = $"{keyPrefix}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await _s3Client.PutObjectAsync(request);

        var region = _s3Client.Config.RegionEndpoint?.SystemName ?? "us-east-1";
        return $"https://{_bucketName}.s3.{region}.amazonaws.com/{key}";
    }
}
