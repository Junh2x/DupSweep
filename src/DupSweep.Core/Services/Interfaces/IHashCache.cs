namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 해시 캐시 서비스 인터페이스
/// 계산된 해시값을 로컬에 캐싱하여 재계산 방지
/// </summary>
public interface IHashCache
{
    /// <summary>
    /// 빠른 해시 조회 (파일이 변경되었으면 null 반환)
    /// </summary>
    Task<string?> TryGetQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);

    /// <summary>
    /// 전체 해시 조회 (파일이 변경되었으면 null 반환)
    /// </summary>
    Task<string?> TryGetFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);

    /// <summary>
    /// 빠른 해시 저장
    /// </summary>
    Task SaveQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken);

    /// <summary>
    /// 전체 해시 저장
    /// </summary>
    Task SaveFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken);

    /// <summary>
    /// 모든 캐시 삭제
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken);
}
