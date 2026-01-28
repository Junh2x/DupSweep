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
        _logger.LogInformation("병렬 처리 ?�션???�데?�트?�었?�니??");
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

        _logger.LogDebug("CPU 바운??병렬 처리 ?�작 - 병렬?? {Parallelism}", parallelism);

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

        _logger.LogDebug("CPU 바운??병렬 처리 (결과 반환) ?�작 - 병렬?? {Parallelism}", parallelism);

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

        _logger.LogDebug("I/O 바운??병렬 처리 ?�작 - ?�라?�브 ?�형: {StorageDriveType}, 병렬?? {Parallelism}",
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

        // ?�일 ?�기�?분류
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
            "?�일 ?�기 최적??병렬 처리 - ?�형: {Small}, 중형: {Medium}, ?�?? {Large}, ?�라?�브: {StorageDriveType}",
            smallFiles.Count, mediumFiles.Count, largeFiles.Count, driveType);

        // ?��? ?�일 ?�선 처리 (빠른 ?�드�?
        if (_options.EnableSmallFilesPriority && smallFiles.Count > 0)
        {
            var smallParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(smallFiles, body, smallParallelism, cancellationToken);
        }

        // 중간 ?�기 ?�일 처리
        if (mediumFiles.Count > 0)
        {
            var mediumParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(mediumFiles, body, mediumParallelism, cancellationToken);
        }

        // ?�?�량 ?�일 처리 (병렬???�한)
        if (largeFiles.Count > 0)
        {
            var largeParallelism = Math.Min(_options.LargeFileParallelism, GetAdaptiveParallelism(driveType));
            await ProcessBatchAsync(largeFiles, body, largeParallelism, cancellationToken);
        }

        // ?��? ?�일 ?�선 처리가 비활?�화??경우
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

            // 캐시 ?�인
            if (_driveTypeCache.TryGetValue(root, out var cachedType))
                return cachedType;

            // ?�트?�크 ?�라?�브 ?�인
            if (root.StartsWith(@"\\"))
            {
                _driveTypeCache[root] = StorageDriveType.Network;
                return StorageDriveType.Network;
            }

            var driveInfo = new DriveInfo(root);

            // ?�라?�브 ?�형 ?�인
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

            // SSD vs HDD ?�별 (Windows only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var isSsd = DetectSsdWindows(root);
                var detectedType = isSsd ? StorageDriveType.Ssd : StorageDriveType.Hdd;
                _driveTypeCache[root] = detectedType;
                return detectedType;
            }

            // 기본�?
            _driveTypeCache[root] = StorageDriveType.Unknown;
            return StorageDriveType.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("?�라?�브 ?�형 감�? ?�패: {Path}, ?�류: {Error}", path, ex.Message);
            return StorageDriveType.Unknown;
        }
    }

    private bool DetectSsdWindows(string root)
    {
        try
        {
            var driveLetter = root.TrimEnd('\\', ':');

            // WMI�??�용?�여 ?�라?�브 ?�형 ?�인
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
            // WMI ?�근 ?�패 ??기본�?반환
        }

        // 기본?�으�?SSD�?가??(?�능???�전???�택)
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
        // 캐시??�??�용 (?�무 ?�주 조회?��? ?�도�?
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
            // GC ??메모�?
            usage.GcHeapBytes = GC.GetTotalMemory(false);

            // ?�로?�스 메모�?
            using var process = Process.GetCurrentProcess();
            usage.UsedMemoryBytes = process.WorkingSet64;

            // ?�스??메모�?(Windows)
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

            // CPU ?�용�?(간접 측정)
            usage.CpuUsagePercent = EstimateCpuUsage();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("리소???�용??조회 ?�패: {Error}", ex.Message);
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

        // 리소??과�?????병렬??감소
        if (resourceUsage.IsOverloaded(_options))
        {
            var reducedParallelism = Math.Max(1, baseParallelism / 2);
            _logger.LogDebug(
                "리소??과�???감�? - 병렬??감소: {Base} -> {Reduced}, CPU: {Cpu:F1}%, Memory: {Memory:F1}%",
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
            // 짧�? ?�기로 ?�스??부??감소
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
