namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IImageStorageService
{
    Task<string> SaveImageAsync(byte[] imageBytes, string fileName, Guid contentId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetImageAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> DeleteImageAsync(string filePath, CancellationToken cancellationToken = default);
    Task<byte[]> CompressImageAsync(byte[] imageBytes, int maxWidth = 800, int maxHeight = 600, int quality = 85, CancellationToken cancellationToken = default);
}
