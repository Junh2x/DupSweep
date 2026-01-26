using System.IO.Hashing;
using Blake3;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.Infrastructure.Hashing;

public class HashService : IHashService
{
    private const int QuickHashSize = 64 * 1024;
    private const int BufferSize = 16 * 1024;

    public async Task<string> ComputeQuickHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: QuickHashSize,
            FileOptions.SequentialScan);

        var buffer = new byte[QuickHashSize];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

        var hasher = new XxHash64();
        hasher.Append(buffer.AsSpan(0, read));
        return Convert.ToHexString(hasher.GetCurrentHash());
    }

    public async Task<string> ComputeFullHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: BufferSize,
            FileOptions.SequentialScan);

        using var hasher = Hasher.New();
        var buffer = new byte[BufferSize];
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            hasher.Update(buffer.AsSpan(0, read));
        }

        var hash = hasher.Finalize();
        return Convert.ToHexString(hash.AsSpan());
    }
}
