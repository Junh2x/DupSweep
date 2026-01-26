namespace DupSweep.Core.Services.Interfaces;

public interface IThumbnailCache
{
    Task<byte[]?> TryGetAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);
    Task SaveAsync(string filePath, long fileSize, DateTime lastWriteTime, byte[] data, CancellationToken cancellationToken);
}
