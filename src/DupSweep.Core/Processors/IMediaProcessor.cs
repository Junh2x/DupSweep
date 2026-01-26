using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

public interface IImageProcessor
{
    Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
    Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
}

public interface IVideoProcessor
{
    Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
    Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
}

public interface IAudioProcessor
{
    Task<ulong?> ComputeFingerprintAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
}
