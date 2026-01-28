using DupSweep.Core.Services.Interfaces;
using LiteDB;

namespace DupSweep.Infrastructure.Caching;

/// <summary>
/// 해시 캐시 서비스 구현
/// LiteDB를 사용하여 계산된 해시값을 로컬에 캐싱
/// </summary>
public class HashCache : IHashCache
{
    private readonly string _dbPath;
    private readonly object _lock = new();

    public HashCache()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DupSweep", "cache");
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "hashes.db");
    }

    /// <summary>
    /// 캐시에서 빠른 해시 조회
    /// </summary>
    public Task<string?> TryGetQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<HashRecord>("quickhashes");
                var record = col.FindById(filePath);
                if (record == null)
                {
                    return (string?)null;
                }

                // 파일이 변경되었으면 캐시 삭제
                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (string?)null;
                }

                return record.Hash;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 캐시에서 전체 해시 조회
    /// </summary>
    public Task<string?> TryGetFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<HashRecord>("fullhashes");
                var record = col.FindById(filePath);
                if (record == null)
                {
                    return (string?)null;
                }

                // 파일이 변경되었으면 캐시 삭제
                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (string?)null;
                }

                return record.Hash;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 캐시에 빠른 해시 저장
    /// </summary>
    public Task SaveQuickHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<HashRecord>("quickhashes");
                var record = new HashRecord
                {
                    FilePath = filePath,
                    FileSize = fileSize,
                    LastWriteTicks = lastWriteTime.Ticks,
                    Hash = hash
                };

                col.Upsert(record);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 캐시에 전체 해시 저장
    /// </summary>
    public Task SaveFullHashAsync(string filePath, long fileSize, DateTime lastWriteTime, string hash, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                var col = db.GetCollection<HashRecord>("fullhashes");
                var record = new HashRecord
                {
                    FilePath = filePath,
                    FileSize = fileSize,
                    LastWriteTicks = lastWriteTime.Ticks,
                    Hash = hash
                };

                col.Upsert(record);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 모든 해시 캐시 삭제
    /// </summary>
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                using var db = new LiteDatabase(_dbPath);
                db.DropCollection("quickhashes");
                db.DropCollection("fullhashes");
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 해시 캐시 레코드
    /// </summary>
    private sealed class HashRecord
    {
        [BsonId]
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public string Hash { get; set; } = string.Empty;
    }
}
