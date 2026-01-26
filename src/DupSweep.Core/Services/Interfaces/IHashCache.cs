namespace DupSweep.Core.Services.Interfaces;

public interface IHashCache
{
    Task<string?> TryGetQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);
    Task<string?> TryGetFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);
    Task SaveQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken);
    Task SaveFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
