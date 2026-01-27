using DupSweep.Core.Models;

namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 병렬 처리 실행기 인터페이스.
/// 드라이브 유형과 시스템 리소스에 따라 최적화된 병렬 처리를 제공합니다.
/// </summary>
public interface IParallelExecutor
{
    /// <summary>
    /// 현재 병렬 처리 옵션
    /// </summary>
    ParallelProcessingOptions Options { get; }

    /// <summary>
    /// 병렬 처리 옵션을 업데이트합니다.
    /// </summary>
    void UpdateOptions(ParallelProcessingOptions options);

    /// <summary>
    /// CPU 바운드 작업을 병렬로 실행합니다.
    /// </summary>
    Task ForEachCpuBoundAsync<T>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CPU 바운드 작업을 병렬로 실행하고 결과를 반환합니다.
    /// </summary>
    Task<IEnumerable<TResult>> ForEachCpuBoundAsync<T, TResult>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task<TResult>> body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// I/O 바운드 작업을 병렬로 실행합니다.
    /// </summary>
    Task ForEachIoBoundAsync<T>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        string? drivePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 파일 크기에 따라 최적화된 병렬 처리를 수행합니다.
    /// </summary>
    Task ForEachFileSizeOptimizedAsync<T>(
        IEnumerable<T> source,
        Func<T, long> sizeSelector,
        Func<T, CancellationToken, Task> body,
        string? drivePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 경로의 드라이브 유형을 감지합니다.
    /// </summary>
    StorageDriveType DetectStorageDriveType(string path);

    /// <summary>
    /// 현재 시스템 리소스 사용량을 가져옵니다.
    /// </summary>
    ResourceUsage GetCurrentResourceUsage();

    /// <summary>
    /// 리소스 사용량에 따라 병렬도를 동적으로 조절합니다.
    /// </summary>
    int GetAdaptiveParallelism(StorageDriveType driveType);
}

/// <summary>
/// 시스템 리소스 사용량
/// </summary>
public class ResourceUsage
{
    /// <summary>
    /// CPU 사용률 (0-100)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// 메모리 사용률 (0-100)
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    /// <summary>
    /// 사용된 메모리 (바이트)
    /// </summary>
    public long UsedMemoryBytes { get; set; }

    /// <summary>
    /// 사용 가능한 메모리 (바이트)
    /// </summary>
    public long AvailableMemoryBytes { get; set; }

    /// <summary>
    /// GC 힙 메모리 (바이트)
    /// </summary>
    public long GcHeapBytes { get; set; }

    /// <summary>
    /// 측정 시간
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 리소스 과부하 상태인지 여부
    /// </summary>
    public bool IsOverloaded(ParallelProcessingOptions options)
    {
        if (options.MaxCpuUsagePercent > 0 && CpuUsagePercent > options.MaxCpuUsagePercent)
            return true;

        if (options.MaxMemoryUsagePercent > 0 && MemoryUsagePercent > options.MaxMemoryUsagePercent)
            return true;

        return false;
    }
}
