namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 해시 계산 서비스 인터페이스
/// 파일의 빠른 해시(부분)와 전체 해시 계산
/// </summary>
public interface IHashService
{
    /// <summary>
    /// 빠른 해시 계산 (파일의 일부만 사용)
    /// </summary>
    Task<string> ComputeQuickHashAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// 전체 해시 계산 (파일 전체 사용)
    /// </summary>
    Task<string> ComputeFullHashAsync(string filePath, CancellationToken cancellationToken);
}
