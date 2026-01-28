using DupSweep.Core.Services.Interfaces;
using LiteDB;

namespace DupSweep.Infrastructure.Caching;

/// <summary>
/// 썸네일 캐시 서비스 구현
/// LiteDB를 사용하여 생성된 썸네일을 로컬에 캐싱
/// </summary>
public class ThumbnailCache : IThumbnailCache
{
    private readonly string _dbPath;
    private readonly object _lock = new();

    public ThumbnailCache()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DupSweep", "cache");
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "thumbnails.db");
    }

    /// <summary>
    /// 캐시에서 썸네일 조회
    /// 파일 크기/수정일이 변경되었으면 캐시 무효화
    /// </summary>
    public Task<byte[]?> TryGetAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<ThumbnailRecord>("thumbnails");
                var record = col.FindById(filePath);
                if (record == null)
                {
                    return (byte[]?)null;
                }

                // 파일이 변경되었으면 캐시 삭제
                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (byte[]?)null;
                }

                return record.Data;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 캐시에 썸네일 저장
    /// </summary>
    public Task SaveAsync(string filePath, long fileSize, DateTime lastWriteTime, byte[] data, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<ThumbnailRecord>("thumbnails");
                var record = new ThumbnailRecord
                {
                    FilePath = filePath,
                    FileSize = fileSize,
                    LastWriteTicks = lastWriteTime.Ticks,
                    Data = data
                };

                col.Upsert(record);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 썸네일 캐시 레코드
    /// </summary>
    private sealed class ThumbnailRecord
    {
        [BsonId]
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
