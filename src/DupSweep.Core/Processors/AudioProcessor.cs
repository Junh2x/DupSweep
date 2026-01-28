using System.Diagnostics;
using System.IO.Hashing;
using DupSweep.Core.Models;

namespace DupSweep.Core.Processors;

/// <summary>
/// 오디오 처리기
/// FFmpeg를 사용하여 PCM 추출 후 XxHash64로 핑거프린트 생성
/// </summary>
public class AudioProcessor : IAudioProcessor
{
    // PCM 변환 설정
    private const int SampleRate = 8000;    // 샘플레이트 (Hz)
    private const int Channels = 1;          // 모노 채널
    private const int BytesPerSample = 2;    // 16비트 샘플
    private const int SegmentSeconds = 1;    // 세그먼트 단위 (초)
    private const int MaxSeconds = 60;       // 최대 분석 길이 (초)

    /// <summary>
    /// 오디오 핑거프린트 계산
    /// 오디오를 PCM으로 변환 후 XxHash64로 해시
    /// </summary>
    public async Task<ulong?> ComputeFingerprintAsync(string filePath, ScanConfig config, CancellationToken cancellationToken)
    {
        // FFmpeg 존재 확인
        var ffmpeg = ResolveFfmpegPath(config);
        if (!File.Exists(ffmpeg) && !IsToolInPath(ffmpeg))
        {
            return null;
        }

        var tempDir = CreateTempDirectory();
        var pcmPath = Path.Combine(tempDir, "audio.pcm");

        try
        {
            // 오디오를 PCM으로 변환
            if (!ExtractPcm(filePath, pcmPath, config))
            {
                return null;
            }

            // PCM 데이터 해싱
            await using var stream = new FileStream(pcmPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var segmentBytes = SampleRate * Channels * BytesPerSample * SegmentSeconds;
            var buffer = new byte[segmentBytes];

            var hasher = new XxHash64();
            int secondsRead = 0;

            while (secondsRead < MaxSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                hasher.Append(buffer.AsSpan(0, read));
                secondsRead++;
            }

            var hashBytes = hasher.GetCurrentHash();
            return BitConverter.ToUInt64(hashBytes, 0);
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
    /// FFmpeg로 오디오를 PCM으로 변환
    /// </summary>
    private static bool ExtractPcm(string filePath, string outputPath, ScanConfig config)
    {
        var ffmpeg = ResolveFfmpegPath(config);
        if (string.IsNullOrWhiteSpace(ffmpeg))
        {
            return false;
        }

        var args = $"-y -i \"{filePath}\" -ac {Channels} -ar {SampleRate} -t {MaxSeconds} -f s16le \"{outputPath}\"";
        var exitCode = RunProcessForExit(ffmpeg, args);
        return exitCode == 0 && File.Exists(outputPath);
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

    private static int RunProcessForExit(string fileName, string arguments, int timeoutMs = 60000)
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

    private static bool IsToolInPath(string toolName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = toolName,
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            return process.WaitForExit(5000);
        }
        catch
        {
            return false;
        }
    }
}
