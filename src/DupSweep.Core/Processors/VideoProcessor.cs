using System.Diagnostics;
using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

/// <summary>
/// 비디오 처리기
/// FFmpeg를 사용하여 키프레임 추출 및 분석
/// </summary>
public class VideoProcessor : IVideoProcessor
{
    private readonly ImageProcessor _imageProcessor = new();

    /// <summary>
    /// 비디오 지각 해시 계산
    /// 여러 위치에서 키프레임을 추출하여 이미지 해시 후 병합
    /// </summary>
    public async Task<ulong?> ComputePerceptualHashAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        try
        {
            // FFmpeg 존재 확인
            var ffmpeg = ResolveFfmpegPath(config);
            if (!File.Exists(ffmpeg) && !IsToolInPath(ffmpeg))
            {
                return null;
            }

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
                // 각 위치에서 프레임 추출 및 해시 계산
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

            // 여러 해시를 병합하여 최종 해시 생성
            return MergeHashes(hashes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 비디오 썸네일 생성
    /// 중간 지점에서 프레임 추출
    /// </summary>
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

    /// <summary>
    /// FFprobe로 비디오 길이 추출
    /// </summary>
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

    /// <summary>
    /// 프레임 추출 위치 계산 (25%, 50%, 75%)
    /// </summary>
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

    /// <summary>
    /// FFmpeg로 특정 위치의 프레임 추출
    /// </summary>
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

    /// <summary>
    /// 여러 해시를 다수결 방식으로 병합
    /// </summary>
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

            // 과반수 이상이면 해당 비트를 1로 설정
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
        catch { }
    }

    private static string ResolveFfmpegPath(ScanConfig config)
    {
        return ResolveToolPath(config.FfmpegPath, "ffmpeg.exe", "ffmpeg");
    }

    private static string ResolveFfprobePath(ScanConfig config)
    {
        return ResolveToolPath(config.FfprobePath, "ffprobe.exe", "ffprobe");
    }

    /// <summary>
    /// 외부 도구 경로 해석 (설정 > 번들 > PATH)
    /// </summary>
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

    private static int RunProcessForExit(string fileName, string arguments, int timeoutMs = 30000)
    {
        try
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
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return -1;
            }
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string? RunProcess(string fileName, string arguments, int timeoutMs = 10000)
    {
        try
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
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                return null;
            }
            return output;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsToolInPath(string toolName)
    {
        try
        {
            var result = RunProcess(toolName, "-version", 5000);
            return result != null;
        }
        catch
        {
            return false;
        }
    }
}
