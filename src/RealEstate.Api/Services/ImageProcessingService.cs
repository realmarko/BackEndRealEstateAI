using SkiaSharp;

namespace RealEstate.Api.Services;

public class ProcessedImage
{
    public required Stream Content { get; init; }
    public string ContentType { get; init; } = "image/jpeg";
    public string FileExtension { get; init; } = ".jpg";
}

// Thrown when a file passed the content-type check but SkiaSharp can't actually decode/process it
// as an image (corrupt upload, mislabeled file, absurd pixel dimensions) — callers should treat
// this the same as "invalid photo type", not let it surface as an unhandled 500/502.
public class ImageProcessingException : Exception
{
    public ImageProcessingException(string message, Exception inner) : base(message, inner) { }
}

public interface IImageProcessingService
{
    Task<ProcessedImage> ProcessAsync(IFormFile photo, CancellationToken cancellationToken = default);
}

// Every photo (listing or agent) is resized and recompressed here before it ever reaches S3 —
// a phone photo can be 4000x3000px and several MB, but every visitor who loads a listing
// downloads the stored file in full for what's shown as a card thumbnail or a modest gallery
// image. Always normalizes to JPEG regardless of input format (PNG/WebP in, JPEG out) — the
// simplest single-format choice; WebP output would compress further but adds a format-
// negotiation concern not worth taking on for this first pass.
public class ImageProcessingService : IImageProcessingService
{
    private const int MaxDimensionPx = 1920;
    private const int JpegQuality = 82;

    // Bounds decoded pixel dimensions, checked from the codec header before the full pixel
    // buffer is allocated — a small (<5MB, per PhotoUploadService's MaxPhotoBytes) file can
    // still declare huge dimensions (a "decompression bomb"), which would otherwise allocate
    // width*height*4 bytes (900MB+ for 15000x15000) before ResizeIfNeeded ever runs.
    private const int MaxDecodedDimensionPx = 10_000;

    // Mitchell is a standard high-quality choice for photographic downscaling — noticeably
    // sharper than SKSamplingOptions.Default, which is nearest-neighbor and visibly aliases
    // fine detail (tile, brick, blinds) common in real estate photos.
    private static readonly SKSamplingOptions ResizeSampling = new(SKCubicResampler.Mitchell);

    public Task<ProcessedImage> ProcessAsync(IFormFile photo, CancellationToken cancellationToken = default)
    {
        using var inputStream = photo.OpenReadStream();

        SKCodec codec;
        try
        {
            codec = SKCodec.Create(inputStream) ?? throw new InvalidOperationException("SKCodec.Create returned null.");
        }
        catch (Exception ex)
        {
            throw new ImageProcessingException($"Could not decode '{photo.FileName}' as an image.", ex);
        }

        using (codec)
        {
            if (codec.Info.Width > MaxDecodedDimensionPx || codec.Info.Height > MaxDecodedDimensionPx)
            {
                throw new ImageProcessingException(
                    $"'{photo.FileName}' is {codec.Info.Width}x{codec.Info.Height}px, which exceeds the {MaxDecodedDimensionPx}px limit.",
                    new InvalidOperationException("Decoded dimensions exceed MaxDecodedDimensionPx."));
            }

            SKBitmap decoded;
            try
            {
                decoded = SKBitmap.Decode(codec) ?? throw new InvalidOperationException("SKBitmap.Decode returned null.");
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"Could not decode '{photo.FileName}' as an image.", ex);
            }

            // Resize and encoding can also legitimately fail on a decodable-but-unusual bitmap
            // (an exotic color type JPEG can't encode, an allocation failure on resize) — those
            // must map to the same "invalid photo" response as a decode failure, not fall through
            // to PhotoUploadService's generic catch, which reports a misleading transient 502.
            try
            {
                using var oriented = ApplyExifOrientation(decoded, codec.EncodedOrigin);
                using var resized = ResizeIfNeeded(oriented);
                var toEncode = resized ?? oriented;

                using var image = SKImage.FromBitmap(toEncode);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
                    ?? throw new InvalidOperationException("SKImage.Encode returned null.");

                var output = new MemoryStream();
                encoded.SaveTo(output);
                output.Position = 0;

                return Task.FromResult(new ProcessedImage { Content = output });
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"Could not process '{photo.FileName}' as an image.", ex);
            }
        }
    }

    // Phone cameras nearly always store landscape pixel data plus an EXIF orientation tag rather
    // than pre-rotating it; SKBitmap.Decode ignores that tag and JPEG re-encoding drops it
    // entirely, so without this every portrait phone photo would come out sideways once stored.
    // Handles the three rotations real cameras produce (90/180/270); the four mirrored origins
    // (rare — mirrored scans) are left as-is rather than adding untested flip-matrix code for a
    // case phone cameras don't produce.
    private static readonly SKSamplingOptions OrientSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        SKBitmap rotated;
        switch (origin)
        {
            case SKEncodedOrigin.BottomRight: // 180°
                rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0, OrientSampling);
                }
                break;
            case SKEncodedOrigin.RightTop: // 90° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(rotated.Width, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0, OrientSampling);
                }
                break;
            case SKEncodedOrigin.LeftBottom: // 90° CCW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Translate(0, rotated.Height);
                    canvas.RotateDegrees(-90);
                    canvas.DrawBitmap(bitmap, 0, 0, OrientSampling);
                }
                break;
            default:
                return bitmap;
        }

        bitmap.Dispose();
        return rotated;
    }

    // Returns null (caller falls back to the original bitmap) when no resize is needed, so a
    // photo that's already small never gets upscaled or re-decoded for nothing.
    private static SKBitmap? ResizeIfNeeded(SKBitmap original)
    {
        if (original.Width <= MaxDimensionPx && original.Height <= MaxDimensionPx) return null;

        var scale = MaxDimensionPx / (float)Math.Max(original.Width, original.Height);
        var newWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
        var newHeight = Math.Max(1, (int)Math.Round(original.Height * scale));

        return original.Resize(new SKImageInfo(newWidth, newHeight), ResizeSampling)
            ?? throw new InvalidOperationException("SKBitmap.Resize returned null.");
    }
}
