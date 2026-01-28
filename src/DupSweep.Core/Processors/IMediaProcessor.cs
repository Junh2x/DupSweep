using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

/// <summary>
/// 이미지 처리 인터페이스
/// 지각 해시 계산, 썸네일 생성, 해상도 추출
/// </summary>
public interface IImageProcessor
{
    Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
    Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
    Task<(int Width, int Height)> GetImageResolutionAsync(string filePath, CancellationToken cancellationToken);
}

/// <summary>
/// 비디오 처리 인터페이스
/// 키프레임 기반 지각 해시 계산, 썸네일 생성
/// </summary>
public interface IVideoProcessor
{
    Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
    Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
}

/// <summary>
/// 오디오 처리 인터페이스
/// 오디오 핑거프린트 계산
/// </summary>
public interface IAudioProcessor
{
    Task<ulong?> ComputeFingerprintAsync(string filePath, ScanConfig config, CancellationToken cancellationToken);
}
