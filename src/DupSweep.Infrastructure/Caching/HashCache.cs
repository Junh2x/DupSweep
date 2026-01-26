using DupSweep.Core.Services.Interfaces;
using LiteDB;

namespace DupSweep.Infrastructure.Caching;

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

                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (string?)null;
                }

                return record.Hash;
            }
        }, cancellationToken);
    }

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

                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (string?)null;
                }

                return record.Hash;
            }
        }, cancellationToken);
    }

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

    private sealed class HashRecord
    {
        [BsonId]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public string Hash { get; set; } = string.Empty;
    }
}
