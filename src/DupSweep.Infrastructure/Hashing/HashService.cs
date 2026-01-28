using System.IO.Hashing;
using Blake3;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.Infrastructure.Hashing;

/// <summary>
/// 해시 계산 서비스 구현
/// XxHash64 (빠른 해시) 및 BLAKE3 (전체 해시) 알고리즘 사용
/// </summary>
public class HashService : IHashService
{
    private const int QuickHashSize = 64 * 1024;  // 빠른 해시용 64KB
    private const int BufferSize = 16 * 1024;     // 읽기 버퍼 16KB

    /// <summary>
    /// 빠른 해시 계산 (파일 앞부분 64KB만 사용)
    /// XxHash64 알고리즘으로 빠른 비교용 해시 생성
    /// </summary>
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

    /// <summary>
    /// 전체 해시 계산 (파일 전체 사용)
    /// BLAKE3 알고리즘으로 정확한 비교용 해시 생성
    /// </summary>
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
