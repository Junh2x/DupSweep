namespace DupSweep.Core.Services.Interfaces;

public interface IHashService
{
    Task<string> ComputeQuickHashAsync(string filePath, CancellationToken cancellationToken);
    Task<string> ComputeFullHashAsync(string filePath, CancellationToken cancellationToken);
}
