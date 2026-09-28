using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstate.Api.Services;
using Xunit;

namespace RealEstate.Api.Tests.Services;

public class PhotoUploadServiceTests
{
    private readonly Mock<IS3UploadService> _s3Service = new();
    private readonly Mock<IImageProcessingService> _imageProcessingService = new();
    private readonly Mock<ILogger<PhotoUploadService>> _logger = new();

    private PhotoUploadService BuildService() =>
        new(_s3Service.Object, _imageProcessingService.Object, _logger.Object);

    private static Mock<IFormFile> BuildPhoto(string contentType = "image/jpeg", long length = 1024)
    {
        var photo = new Mock<IFormFile>();
        photo.Setup(p => p.ContentType).Returns(contentType);
        photo.Setup(p => p.Length).Returns(length);
        return photo;
    }

    private static ProcessedImage BuildProcessedImage() => new()
    {
        Content = new MemoryStream(new byte[] { 1, 2, 3 }),
        ContentType = "image/jpeg",
        FileExtension = ".jpg"
    };

    [Fact]
    public async Task UploadManyAsync_RejectsUnsupportedContentType()
    {
        var service = BuildService();
        var photo = BuildPhoto(contentType: "application/pdf");

        var result = await service.UploadManyAsync(new List<IFormFile> { photo.Object }, "listings/1");

        Assert.Equal(PhotoUploadErrorKind.InvalidType, result.ErrorKind);
        _imageProcessingService.Verify(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadManyAsync_RejectsPhotoLargerThanFiveMegabytes()
    {
        var service = BuildService();
        var photo = BuildPhoto(length: 6 * 1024 * 1024);

        var result = await service.UploadManyAsync(new List<IFormFile> { photo.Object }, "listings/1");

        Assert.Equal(PhotoUploadErrorKind.TooLarge, result.ErrorKind);
        _imageProcessingService.Verify(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadManyAsync_ValidatesAllPhotosBeforeUploadingAny()
    {
        var service = BuildService();
        var validPhoto = BuildPhoto();
        var invalidPhoto = BuildPhoto(contentType: "application/pdf");

        var result = await service.UploadManyAsync(
            new List<IFormFile> { validPhoto.Object, invalidPhoto.Object }, "listings/1");

        Assert.Equal(PhotoUploadErrorKind.InvalidType, result.ErrorKind);
        _imageProcessingService.Verify(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadManyAsync_UploadsProcessedContentForEachPhoto()
    {
        _imageProcessingService
            .Setup(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProcessedImage);
        _s3Service
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), "image/jpeg", ".jpg", "listings/1"))
            .ReturnsAsync("https://cdn.example.com/photo.jpg");

        var service = BuildService();
        var photo = BuildPhoto();

        var result = await service.UploadManyAsync(new List<IFormFile> { photo.Object }, "listings/1");

        Assert.Null(result.ErrorKind);
        Assert.Equal(new[] { "https://cdn.example.com/photo.jpg" }, result.Urls);
    }

    [Fact]
    public async Task UploadManyAsync_RollsBackAlreadyUploadedPhotosWhenALaterOneFailsToUpload()
    {
        _imageProcessingService
            .Setup(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProcessedImage);
        _s3Service
            .SetupSequence(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "listings/1"))
            .ReturnsAsync("https://cdn.example.com/first.jpg")
            .ThrowsAsync(new Exception("S3 is down"));

        var service = BuildService();
        var photos = new List<IFormFile> { BuildPhoto().Object, BuildPhoto().Object };

        var result = await service.UploadManyAsync(photos, "listings/1");

        Assert.Equal(PhotoUploadErrorKind.UploadFailed, result.ErrorKind);
        _s3Service.Verify(s => s.DeleteFileAsync("https://cdn.example.com/first.jpg"), Times.Once);
    }

    [Fact]
    public async Task UploadManyAsync_ReturnsInvalidTypeWhenImageProcessingFails()
    {
        _imageProcessingService
            .Setup(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ImageProcessingException("bad image", new InvalidOperationException()));

        var service = BuildService();
        var photo = BuildPhoto();

        var result = await service.UploadManyAsync(new List<IFormFile> { photo.Object }, "listings/1");

        Assert.Equal(PhotoUploadErrorKind.InvalidType, result.ErrorKind);
        _s3Service.Verify(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UploadAsync_DelegatesToUploadManyAsyncWithASinglePhoto()
    {
        _imageProcessingService
            .Setup(s => s.ProcessAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildProcessedImage);
        _s3Service
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), "image/jpeg", ".jpg", "agents/1"))
            .ReturnsAsync("https://cdn.example.com/agent.jpg");

        var service = BuildService();
        var photo = BuildPhoto();

        var result = await service.UploadAsync(photo.Object, "agents/1");

        Assert.Null(result.ErrorKind);
        Assert.Equal(new[] { "https://cdn.example.com/agent.jpg" }, result.Urls);
    }
}
