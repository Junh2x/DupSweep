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
/// Î≥ëÎ†¨ Ï≤òÎ¶¨ ?§ÌñâÍ∏?Íµ¨ÌòÑ.
/// ?úÎùº?¥Î∏å ?†ÌòïÍ≥??úÏä§??Î¶¨ÏÜå?§Ïóê ?∞Îùº ÏµúÏ†Å?îÎêú Î≥ëÎ†¨ Ï≤òÎ¶¨Î•??úÍ≥µ?©Îãà??
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
        _logger.LogInformation("Î≥ëÎ†¨ Ï≤òÎ¶¨ ?µÏÖò???ÖÎç∞?¥Ìä∏?òÏóà?µÎãà??");
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

        _logger.LogDebug("CPU Î∞îÏö¥??Î≥ëÎ†¨ Ï≤òÎ¶¨ ?úÏûë - Î≥ëÎ†¨?? {Parallelism}", parallelism);

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

        _logger.LogDebug("CPU Î∞îÏö¥??Î≥ëÎ†¨ Ï≤òÎ¶¨ (Í≤∞Í≥º Î∞òÌôò) ?úÏûë - Î≥ëÎ†¨?? {Parallelism}", parallelism);

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

        _logger.LogDebug("I/O Î∞îÏö¥??Î≥ëÎ†¨ Ï≤òÎ¶¨ ?úÏûë - ?úÎùº?¥Î∏å ?†Ìòï: {StorageDriveType}, Î≥ëÎ†¨?? {Parallelism}",
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

        // ?åÏùº ?¨Í∏∞Î≥?Î∂ÑÎ•ò
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
            "?åÏùº ?¨Í∏∞ ÏµúÏ†Å??Î≥ëÎ†¨ Ï≤òÎ¶¨ - ?åÌòï: {Small}, Ï§ëÌòï: {Medium}, ?Ä?? {Large}, ?úÎùº?¥Î∏å: {StorageDriveType}",
            smallFiles.Count, mediumFiles.Count, largeFiles.Count, driveType);

        // ?ëÏ? ?åÏùº ?∞ÏÑ† Ï≤òÎ¶¨ (Îπ†Î•∏ ?ºÎìúÎ∞?
        if (_options.EnableSmallFilesPriority && smallFiles.Count > 0)
        {
            var smallParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(smallFiles, body, smallParallelism, cancellationToken);
        }

        // Ï§ëÍ∞Ñ ?¨Í∏∞ ?åÏùº Ï≤òÎ¶¨
        if (mediumFiles.Count > 0)
        {
            var mediumParallelism = GetAdaptiveParallelism(driveType);
            await ProcessBatchAsync(mediumFiles, body, mediumParallelism, cancellationToken);
        }

        // ?Ä?©Îüâ ?åÏùº Ï≤òÎ¶¨ (Î≥ëÎ†¨???úÌïú)
        if (largeFiles.Count > 0)
        {
            var largeParallelism = Math.Min(_options.LargeFileParallelism, GetAdaptiveParallelism(driveType));
            await ProcessBatchAsync(largeFiles, body, largeParallelism, cancellationToken);
        }

        // ?ëÏ? ?åÏùº ?∞ÏÑ† Ï≤òÎ¶¨Í∞Ä ÎπÑÌôú?±Ìôî??Í≤ΩÏö∞
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

            // Ï∫êÏãú ?ïÏù∏
            if (_driveTypeCache.TryGetValue(root, out var cachedType))
                return cachedType;

            // ?§Ìä∏?åÌÅ¨ ?úÎùº?¥Î∏å ?ïÏù∏
            if (root.StartsWith(@"\\"))
            {
                _driveTypeCache[root] = StorageDriveType.Network;
                return StorageDriveType.Network;
            }

            var driveInfo = new DriveInfo(root);

            // ?úÎùº?¥Î∏å ?†Ìòï ?ïÏù∏
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

            // SSD vs HDD ?êÎ≥Ñ (Windows only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var isSsd = DetectSsdWindows(root);
                var detectedType = isSsd ? StorageDriveType.Ssd : StorageDriveType.Hdd;
                _driveTypeCache[root] = detectedType;
                return detectedType;
            }

            // Í∏∞Î≥∏Í∞?
            _driveTypeCache[root] = StorageDriveType.Unknown;
            return StorageDriveType.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("?úÎùº?¥Î∏å ?†Ìòï Í∞êÏ? ?§Ìå®: {Path}, ?§Î•ò: {Error}", path, ex.Message);
            return StorageDriveType.Unknown;
        }
    }

    private bool DetectSsdWindows(string root)
    {
        try
        {
            var driveLetter = root.TrimEnd('\\', ':');

            // WMIÎ•??¨Ïö©?òÏó¨ ?úÎùº?¥Î∏å ?†Ìòï ?ïÏù∏
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
            // WMI ?ëÍ∑º ?§Ìå® ??Í∏∞Î≥∏Í∞?Î∞òÌôò
        }

        // Í∏∞Î≥∏?ÅÏúºÎ°?SSDÎ°?Í∞Ä??(?±Îä•???àÏ†Ñ???†ÌÉù)
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
            // Î¨¥Ïãú
        }

        return "0";
    }

    #endregion

    #region Resource Monitoring

    public ResourceUsage GetCurrentResourceUsage()
    {
        // Ï∫êÏãú??Í∞??¨Ïö© (?àÎ¨¥ ?êÏ£º Ï°∞Ìöå?òÏ? ?äÎèÑÎ°?
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
            // GC ??Î©îÎ™®Î¶?
            usage.GcHeapBytes = GC.GetTotalMemory(false);

            // ?ÑÎ°ú?∏Ïä§ Î©îÎ™®Î¶?
            using var process = Process.GetCurrentProcess();
            usage.UsedMemoryBytes = process.WorkingSet64;

            // ?úÏä§??Î©îÎ™®Î¶?(Windows)
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

            // CPU ?¨Ïö©Î•?(Í∞ÑÏ†ë Ï∏°Ï†ï)
            usage.CpuUsagePercent = EstimateCpuUsage();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Î¶¨ÏÜå???¨Ïö©??Ï°∞Ìöå ?§Ìå®: {Error}", ex.Message);
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

        // Î¶¨ÏÜå??Í≥ºÎ?????Î≥ëÎ†¨??Í∞êÏÜå
        if (resourceUsage.IsOverloaded(_options))
        {
            var reducedParallelism = Math.Max(1, baseParallelism / 2);
            _logger.LogDebug(
                "Î¶¨ÏÜå??Í≥ºÎ???Í∞êÏ? - Î≥ëÎ†¨??Í∞êÏÜå: {Base} -> {Reduced}, CPU: {Cpu:F1}%, Memory: {Memory:F1}%",
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
            // ÏßßÏ? ?ÄÍ∏∞Î°ú ?úÏä§??Î∂Ä??Í∞êÏÜå
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
