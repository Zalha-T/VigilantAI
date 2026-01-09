using System.Drawing;
using System.Drawing.Imaging;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ImageStorageService : IImageStorageService
{
    private readonly string _basePath;
    private const int MaxFileSize = 5 * 1024 * 1024; // 5MB

    public ImageStorageService(string basePath = "wwwroot/uploads/images")
    {
        _basePath = basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveImageAsync(byte[] imageBytes, string fileName, Guid contentId, CancellationToken cancellationToken = default)
    {
        // Compress image first
        var compressedBytes = await CompressImageAsync(imageBytes, cancellationToken: cancellationToken);

        // Create content-specific directory
        var contentDir = Path.Combine(_basePath, contentId.ToString());
        Directory.CreateDirectory(contentDir);

        // Generate unique filename
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(contentDir, uniqueFileName);

        // Save file
        await File.WriteAllBytesAsync(filePath, compressedBytes, cancellationToken);

        // Return relative path
        return Path.Combine("uploads", "images", contentId.ToString(), uniqueFileName).Replace('\\', '/');
    }

    public async Task<byte[]?> GetImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine("wwwroot", filePath);
        if (!File.Exists(fullPath))
            return null;

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // filePath is relative (e.g., "uploads/images/{contentId}/{filename}")
            // _basePath is already "wwwroot/uploads/images" or full path
            var fullPath = Path.IsPathRooted(filePath) 
                ? filePath 
                : Path.Combine(_basePath, filePath.Replace("uploads/images/", "").Replace("uploads\\images\\", ""));
            
            if (!File.Exists(fullPath))
            {
                // Try alternative path construction
                var altPath = Path.Combine("wwwroot", filePath);
                if (File.Exists(altPath))
                {
                    File.Delete(altPath);
                    return true;
                }
                return false;
            }

            File.Delete(fullPath);
            
            // Try to delete the content directory if it's empty
            var contentDir = Path.GetDirectoryName(fullPath);
            if (contentDir != null && Directory.Exists(contentDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(contentDir).Any())
                    {
                        Directory.Delete(contentDir);
                    }
                }
                catch
                {
                    // Ignore errors when deleting directory
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting image file {filePath}: {ex.Message}");
            return false;
        }
    }

    public async Task<byte[]> CompressImageAsync(byte[] imageBytes, int maxWidth = 800, int maxHeight = 600, int quality = 85, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("Image bytes cannot be null or empty.", nameof(imageBytes));

            try
            {
                Image image;
                using (var ms = new MemoryStream(imageBytes))
                {
                    image = Image.FromStream(ms);
                }

                try
                {
                    // Calculate new dimensions
                    var ratioX = (double)maxWidth / image.Width;
                    var ratioY = (double)maxHeight / image.Height;
                    var ratio = Math.Min(ratioX, ratioY);

                    var newWidth = (int)(image.Width * ratio);
                    var newHeight = (int)(image.Height * ratio);

                    // If image is already smaller, return original
                    if (newWidth >= image.Width && newHeight >= image.Height)
                        return imageBytes;

                    // Resize image
                    using var resized = new Bitmap(newWidth, newHeight);
                    using var graphics = Graphics.FromImage(resized);
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, 0, 0, newWidth, newHeight);

                    // Save to memory stream with compression
                    using var outputMs = new MemoryStream();
                    var codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                    if (codec != null)
                    {
                        resized.Save(outputMs, codec, encoderParams);
                    }
                    else
                    {
                        resized.Save(outputMs, ImageFormat.Jpeg);
                    }
                    return outputMs.ToArray();
                }
                finally
                {
                    image.Dispose();
                }
            }
            catch (Exception ex)
            {
                // If compression fails, return original bytes
                Console.WriteLine($"Image compression error: {ex.Message}");
                return imageBytes;
            }
        }, cancellationToken);
    }
}
