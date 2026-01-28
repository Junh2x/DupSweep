namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 썸네일 캐시 서비스 인터페이스
/// 생성된 썸네일을 캐싱하여 재생성 방지
/// </summary>
public interface IThumbnailCache
{
    /// <summary>
    /// 캐시에서 썸네일 조회
    /// </summary>
    Task<byte[]?> TryGetAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken);

    /// <summary>
    /// 캐시에 썸네일 저장
    /// </summary>
    Task SaveAsync(string filePath, long fileSize, DateTime lastWriteTime, byte[] data, CancellationToken cancellationToken);
}
