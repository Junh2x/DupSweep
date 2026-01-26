using System.Diagnostics;
using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

public class VideoProcessor : IVideoProcessor
{
    private readonly ImageProcessor _imageProcessor = new();

    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var duration = GetDuration(filePath, config);
            var positions = GetFramePositions(duration);
            if (positions.Count == 0)
            {
                return null;
            }

            var hashes = new List<ulong>();
            var tempDir = CreateTempDirectory();

            try
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var outputPath = Path.Combine(tempDir, $"frame_{i}.jpg");
                    if (!ExtractFrame(filePath, outputPath, positions[i], config))
                    {
                        continue;
                    }

                    var hash = await _imageProcessor.ComputePerceptualHashAsync(outputPath, config, cancellationToken);
                    if (hash.HasValue)
                    {
                        hashes.Add(hash.Value);
                    }
                }
            }
            finally
            {
                TryDeleteDirectory(tempDir);
            }

            if (hashes.Count == 0)
            {
                return null;
            }

            return MergeHashes(hashes);
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> CreateThumbnailAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var position = GetFramePositions(GetDuration(filePath, config)).FirstOrDefault();
            var outputPath = Path.Combine(tempDir, "thumb.jpg");
            if (!ExtractFrame(filePath, outputPath, position, config))
            {
                return null;
            }

            return await _imageProcessor.CreateThumbnailAsync(outputPath, config, cancellationToken);
        }
        catch
        {
            return null;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static TimeSpan? GetDuration(string filePath, ScanConfig config)
    {
        var ffprobe = ResolveFfprobePath(config);
        if (string.IsNullOrWhiteSpace(ffprobe))
        {
            return null;
        }

        var args = $"-v error -show_entries format=duration -of default=nokey=1:noprint_wrappers=1 \"{filePath}\"";
        var output = RunProcess(ffprobe, args);
        if (double.TryParse(output?.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    private static List<TimeSpan> GetFramePositions(TimeSpan? duration)
    {
        if (duration == null || duration.Value.TotalSeconds <= 0)
        {
            return new List<TimeSpan> { TimeSpan.Zero };
        }

        var total = duration.Value.TotalSeconds;
        var positions = new List<TimeSpan>
        {
            TimeSpan.FromSeconds(total * 0.25),
            TimeSpan.FromSeconds(total * 0.5),
            TimeSpan.FromSeconds(total * 0.75)
        };

        return positions;
    }

    private static bool ExtractFrame(string filePath, string outputPath, TimeSpan position, ScanConfig config)
    {
        var ffmpeg = ResolveFfmpegPath(config);
        if (string.IsNullOrWhiteSpace(ffmpeg))
        {
            return false;
        }

        var args = $"-y -ss {position:c} -i \"{filePath}\" -frames:v 1 -q:v 2 \"{outputPath}\"";
        var exitCode = RunProcessForExit(ffmpeg, args);
        return exitCode == 0 && File.Exists(outputPath);
    }

    private static ulong MergeHashes(IReadOnlyList<ulong> hashes)
    {
        ulong merged = 0;
        for (int bit = 0; bit < 64; bit++)
        {
            int setBits = 0;
            for (int i = 0; i < hashes.Count; i++)
            {
                if ((hashes[i] & (1UL << bit)) != 0)
                {
                    setBits++;
                }
            }

            if (setBits >= (hashes.Count + 1) / 2)
            {
                merged |= 1UL << bit;
            }
        }

        return merged;
    }

    private static string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DupSweep", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void TryDeleteDirectory(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private static string ResolveFfmpegPath(ScanConfig config)
    {
        return ResolveToolPath(config.FfmpegPath, "ffmpeg.exe", "ffmpeg");
    }

    private static string ResolveFfprobePath(ScanConfig config)
    {
        return ResolveToolPath(config.FfprobePath, "ffprobe.exe", "ffprobe");
    }

    private static string ResolveToolPath(string? overridePath, string exeName, string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var baseDir = AppContext.BaseDirectory;
        var bundled = Path.Combine(baseDir, "tools", "ffmpeg", exeName);
        if (File.Exists(bundled))
        {
            return bundled;
        }

        var cwdBundled = Path.Combine(Directory.GetCurrentDirectory(), "tools", "ffmpeg", exeName);
        if (File.Exists(cwdBundled))
        {
            return cwdBundled;
        }

        return fallbackName;
    }

    private static int RunProcessForExit(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string? RunProcess(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
