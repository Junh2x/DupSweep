using DupSweep.Core.Services.Interfaces;
using LiteDB;

namespace DupSweep.Infrastructure.Caching;

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

                if (record.FileSize != fileSize || record.LastWriteTicks != lastWriteTime.Ticks)
                {
                    col.Delete(filePath);
                    return (byte[]?)null;
                }

                return record.Data;
            }
        }, cancellationToken);
    }

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

    private sealed class ThumbnailRecord
    {
        [BsonId]
        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }
        public long LastWriteTicks { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
