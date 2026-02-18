using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Services.Interfaces;
using SystemParallel = System.Threading.Tasks.Parallel;
using SystemParallelOptions = System.Threading.Tasks.ParallelOptions;

namespace DupSweep.Infrastructure.Parallel;

/// <summary>
/// 병렬 처리 실행기 구현
/// 드라이브 유형과 시스템 리소스에 따라 최적화된 병렬 처리 제공
/// </summary>
public class ParallelExecutor : IParallelExecutor, IDisposable
{
    private readonly IAppLogger _logger;
    private ParallelProcessingOptions _options;
    private readonly ConcurrentDictionary<string, StorageDriveType> _driveTypeCache = new();
    private readonly SemaphoreSlim _resourceMonitorLock = new(1, 1);
    private ResourceUsage? _lastResourceUsage;
    private DateTime _lastResourceCheck = DateTime.MinValue;
    private bool _disposed;

    public ParallelExecutor(IAppLogger logger, ParallelProcessingOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? ParallelProcessingOptions.Default;
    }

    public ParallelProcessingOptions Options => _options;

    public void UpdateOptions(ParallelProcessingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger.LogInformation("병렬 처리 옵션이 업데이트되었습니다");
    }

    #region CPU Bound Operations

    public async Task ForEachCpuBoundAsync<T>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        CancellationToken cancellationToken = default)
    {
        var parallelism = GetEffectiveCpuParallelism();
        var options = new SystemParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        };

        _logger.LogDebug("CPU 바운드 병렬 처리 시작 - 병렬도: {Parallelism}", parallelism);

        await SystemParallel.ForEachAsync(source, options, async (item, ct) =>
        {
            if (_options.EnableAutoThrottling)
            {
                await ThrottleIfNeededAsync(ct);
            }
            await body(item, ct);
        });
    }

    public async Task<IEnumerable<TResult>> ForEachCpuBoundAsync<T, TResult>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task<TResult>> body,
        CancellationToken cancellationToken = default)
    {
        var parallelism = GetEffectiveCpuParallelism();
        var results = new ConcurrentBag<TResult>();

        _logger.LogDebug("CPU 바운드 병렬 처리 (결과 반환) 시작 - 병렬도: {Parallelism}", parallelism);

        await SystemParallel.ForEachAsync(source, new SystemParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        }, async (item, ct) =>
        {
            if (_options.EnableAutoThrottling)
            {
                await ThrottleIfNeededAsync(ct);
            }
            var result = await body(item, ct);
            results.Add(result);
        });

        return results;
    }

    #endregion

    #region IO Bound Operations

    public async Task ForEachIoBoundAsync<T>(
        IEnumerable<T> source,
        Func<T, CancellationToken, Task> body,
        string? drivePath = null,
        CancellationToken cancellationToken = default)
    {
        var driveType = drivePath != null ? DetectStorageDriveType(drivePath) : StorageDriveType.Unknown;
        var parallelism = GetAdaptiveParallelism(driveType);

        _logger.LogDebug("I/O 바운드 병렬 처리 시작 - 드라이브 유형: {StorageDriveType}, 병렬도: {Parallelism}",
            driveType, parallelism);

        await SystemParallel.ForEachAsync(source, new SystemParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        }, async (item, ct) =>
        {
            if (_options.EnableAutoThrottling)
            {
                await ThrottleIfNeededAsync(ct);
            }
            await body(item, ct);
        });
    }

    #endregion

    #region File Size Optimized Operations

    public async Task ForEachFileSizeOptimizedAsync<T>(
        IEnumerable<T> source,
        Func<T, long> sizeSelector,
        Func<T, CancellationToken, Task> body,
        string? drivePath = null,
        CancellationToken cancellationToken = default)
    {
        var items = source.ToList();
        var driveType = drivePath != null ? DetectStorageDriveType(drivePath) : StorageDriveType.Unknown;

        // 파일 크기별 분류
        var smallFiles = new List<T>();
        var mediumFiles = new List<T>();
        var largeFiles = new List<T>();

        foreach (var item in items)
        {
            var size = sizeSelector(item);
            if (size < _options.SmallFileThreshold)
                smallFiles.Add(item);
            else if (size < _options.LargeFileThreshold)
                mediumFiles.Add(item);
            else
                largeFiles.Add(item);
        }

        _logger.LogDebug(
            "파일 크기 최적화 병렬 처리 - 소형: {Small}, 중형: {Medium}, 대형: {Large}, 드라이브: {StorageDriveType}",
            smallFiles.Count, mediumFiles.Count, largeFiles.Count, driveType);

        // 소형 파일 우선 처리 (빠른 피드백)
        if (_options.EnableSmallFilesPriority && smallFiles.Count > 0)
        {
            var smallParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(smallFiles, body, smallParallelism, cancellationToken);
        }

        // 중간 크기 파일 처리
        if (mediumFiles.Count > 0)
        {
            var mediumParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(mediumFiles, body, mediumParallelism, cancellationToken);
        }

        // 대용량 파일 처리 (병렬도 제한)
        if (largeFiles.Count > 0)
        {
            var largeParallelism = Math.Min(_options.LargeFileParallelism, GetAdaptiveParallelism(driveType));
            await ProcessBatchAsync(largeFiles, body, largeParallelism, cancellationToken);
        }

        // 소형 파일 우선 처리가 비활성화된 경우
        if (!_options.EnableSmallFilesPriority && smallFiles.Count > 0)
        {
            var smallParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(smallFiles, body, smallParallelism, cancellationToken);
        }
    }

    private async Task ProcessBatchAsync<T>(
        IEnumerable<T> items,
        Func<T, CancellationToken, Task> body,
        int parallelism,
        CancellationToken cancellationToken)
    {
        await SystemParallel.ForEachAsync(items, new SystemParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        }, async (item, ct) =>
        {
            if (_options.EnableAutoThrottling)
            {
                await ThrottleIfNeededAsync(ct);
            }
            await body(item, ct);
        });
    }

    #endregion

    #region Drive Detection

    public StorageDriveType DetectStorageDriveType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
                return StorageDriveType.Unknown;

            // 캐시 확인
            if (_driveTypeCache.TryGetValue(root, out var cachedType))
                return cachedType;

            // 네트워크 드라이브 확인
            if (root.StartsWith(@"\\"))
            {
                _driveTypeCache[root] = StorageDriveType.Network;
                return StorageDriveType.Network;
            }

            var driveInfo = new DriveInfo(root);

            // 드라이브 유형 확인
            if (driveInfo.DriveType == System.IO.DriveType.Network)
            {
                _driveTypeCache[root] = StorageDriveType.Network;
                return StorageDriveType.Network;
            }

            if (driveInfo.DriveType == System.IO.DriveType.Removable)
            {
                _driveTypeCache[root] = StorageDriveType.Removable;
                return StorageDriveType.Removable;
            }

            // SSD vs HDD 식별 (Windows only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var isSsd = DetectSsdWindows(root);
                var detectedType = isSsd ? StorageDriveType.Ssd : StorageDriveType.Hdd;
                _driveTypeCache[root] = detectedType;
                return detectedType;
            }

            // 기본값
            _driveTypeCache[root] = StorageDriveType.Unknown;
            return StorageDriveType.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("드라이브 유형 감지 실패: {Path}, 오류: {Error}", path, ex.Message);
            return StorageDriveType.Unknown;
        }
    }

    private bool DetectSsdWindows(string root)
    {
        try
        {
            var driveLetter = root.TrimEnd('\\', ':');

            // WMI를 사용하여 드라이브 유형 확인
            using var searcher = new ManagementObjectSearcher(
                $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceID='{GetPhysicalDiskId(driveLetter)}'");

            searcher.Scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");

            foreach (ManagementObject disk in searcher.Get())
            {
                var mediaType = disk["MediaType"];
                if (mediaType != null)
                {
                    // MediaType: 3 = HDD, 4 = SSD
                    return Convert.ToInt32(mediaType) == 4;
                }
            }
        }
        catch
        {
            // WMI 접근 실패 시 기본값 반환
        }

        // 기본적으로 SSD로 가정 (성능 안전한 선택)
        return true;
    }

    private string GetPhysicalDiskId(string driveLetter)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}:'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

            foreach (ManagementObject partition in searcher.Get())
            {
                using var diskSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject disk in diskSearcher.Get())
                {
                    var index = disk["Index"];
                    if (index != null)
                        return index.ToString()!;
                }
            }
        }
        catch
        {
            // 무시
        }

        return "0";
    }

    #endregion

    #region Resource Monitoring

    public ResourceUsage GetCurrentResourceUsage()
    {
        // 캐시된 값 사용 (너무 자주 조회하지 않도록)
        if (_lastResourceUsage != null &&
            (DateTime.Now - _lastResourceCheck).TotalMilliseconds < _options.ResourceMonitorIntervalMs)
        {
            return _lastResourceUsage;
        }

        var usage = new ResourceUsage
        {
            Timestamp = DateTime.Now
        };

        try
        {
            // GC 힙 메모리
            usage.GcHeapBytes = GC.GetTotalMemory(false);

            // 프로세스 메모리
            using var process = Process.GetCurrentProcess();
            usage.UsedMemoryBytes = process.WorkingSet64;

            // 시스템 메모리 (Windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    usage.AvailableMemoryBytes = (long)memStatus.ullAvailPhys;
                    var totalMemory = (long)memStatus.ullTotalPhys;
                    usage.MemoryUsagePercent = 100.0 * (totalMemory - usage.AvailableMemoryBytes) / totalMemory;
                }
            }

            // CPU 사용률 (간접 측정)
            usage.CpuUsagePercent = EstimateCpuUsage();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("리소스 사용량 조회 실패: {Error}", ex.Message);
        }

        _lastResourceUsage = usage;
        _lastResourceCheck = DateTime.Now;

        return usage;
    }

    private double EstimateCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var startTime = DateTime.Now;
            var startCpuTime = process.TotalProcessorTime;

            Thread.Sleep(50);

            var endTime = DateTime.Now;
            var endCpuTime = process.TotalProcessorTime;

            var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;

            return (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
        }
        catch
        {
            return 0;
        }
    }

    public int GetAdaptiveParallelism(StorageDriveType driveType)
    {
        var baseParallelism = _options.GetIoParallelism(driveType);

        if (!_options.EnableAutoThrottling)
            return baseParallelism;

        var resourceUsage = GetCurrentResourceUsage();

        // 리소스 과부하 시 병렬도 감소
        if (resourceUsage.IsOverloaded(_options))
        {
            var reducedParallelism = Math.Max(1, baseParallelism / 2);
            _logger.LogDebug(
                "리소스 과부하 감지 - 병렬도 감소: {Base} -> {Reduced}, CPU: {Cpu:F1}%, Memory: {Memory:F1}%",
                baseParallelism, reducedParallelism, resourceUsage.CpuUsagePercent, resourceUsage.MemoryUsagePercent);
            return reducedParallelism;
        }

        return baseParallelism;
    }

    private int GetEffectiveCpuParallelism()
    {
        var baseParallelism = _options.GetEffectiveCpuParallelism();

        if (!_options.EnableAutoThrottling)
            return baseParallelism;

        var resourceUsage = GetCurrentResourceUsage();

        if (resourceUsage.IsOverloaded(_options))
        {
            return Math.Max(1, baseParallelism / 2);
        }

        return baseParallelism;
    }

    private async Task ThrottleIfNeededAsync(CancellationToken cancellationToken)
    {
        var resourceUsage = GetCurrentResourceUsage();

        if (resourceUsage.IsOverloaded(_options))
        {
            // 짧은 대기로 시스템 부하 감소
            await Task.Delay(100, cancellationToken);
        }
    }

    #endregion

    #region Native Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _resourceMonitorLock.Dispose();
    }
}
