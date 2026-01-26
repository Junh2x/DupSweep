using System.Diagnostics;
using DupSweep.Core.Logging;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace DupSweep.Infrastructure.Logging;

/// <summary>
/// Serilog 기반 애플리케이션 로거 구현.
/// 도메인 특화 로깅 기능을 제공합니다.
/// </summary>
public class AppLogger : IAppLogger
{
    private readonly ILogger _logger;
    private readonly LoggingConfiguration _config;

    public AppLogger(ILogger<AppLogger> logger, LoggingConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new LoggingConfiguration();
    }

    public ILogger Logger => _logger;

    #region 스캔 관련 로깅

    public void LogScanStarted(string sessionId, IEnumerable<string> directories, string scanMode)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Scan"))
        {
            var dirList = directories.ToList();
            _logger.LogInformation(
                "스캔 시작 - 세션: {SessionId}, 모드: {ScanMode}, 대상 폴더 수: {DirectoryCount}, 폴더: {Directories}",
                sessionId, scanMode, dirList.Count, string.Join(", ", dirList));
        }
    }

    public void LogScanProgress(string sessionId, int processedFiles, int totalFiles, string currentFile)
    {
        if (_config.MinimumLevel <= Core.Logging.LogLevel.Debug)
        {
            _logger.LogDebug(
                "스캔 진행 - 세션: {SessionId}, 진행: {ProcessedFiles}/{TotalFiles}, 현재: {CurrentFile}",
                sessionId, processedFiles, totalFiles, currentFile);
        }
    }

    public void LogScanCompleted(string sessionId, int totalFiles, int duplicateGroups, long potentialSavings, TimeSpan elapsed)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Scan"))
        {
            _logger.LogInformation(
                "스캔 완료 - 세션: {SessionId}, 총 파일: {TotalFiles}, 중복 그룹: {DuplicateGroups}, " +
                "절약 가능 용량: {PotentialSavings:N0} bytes ({PotentialSavingsMB:F2} MB), 소요 시간: {Elapsed}",
                sessionId, totalFiles, duplicateGroups, potentialSavings,
                potentialSavings / (1024.0 * 1024.0), elapsed);
        }
    }

    public void LogScanCancelled(string sessionId, int processedFiles, TimeSpan elapsed)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Scan"))
        {
            _logger.LogWarning(
                "스캔 취소됨 - 세션: {SessionId}, 처리된 파일: {ProcessedFiles}, 소요 시간: {Elapsed}",
                sessionId, processedFiles, elapsed);
        }
    }

    public void LogScanError(string sessionId, Exception exception, string? context = null)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Scan"))
        {
            _logger.LogError(exception,
                "스캔 오류 - 세션: {SessionId}, 컨텍스트: {Context}",
                sessionId, context ?? "N/A");
        }
    }

    #endregion

    #region 삭제 관련 로깅

    public void LogDeletionStarted(string sessionId, int fileCount, long totalSize, bool isPermanent, bool isDryRun)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Deletion"))
        {
            var mode = isDryRun ? "시뮬레이션" : (isPermanent ? "영구 삭제" : "휴지통 이동");
            _logger.LogInformation(
                "삭제 작업 시작 - 세션: {SessionId}, 모드: {Mode}, 파일 수: {FileCount}, " +
                "총 크기: {TotalSize:N0} bytes ({TotalSizeMB:F2} MB), 드라이런: {IsDryRun}",
                sessionId, mode, fileCount, totalSize, totalSize / (1024.0 * 1024.0), isDryRun);
        }
    }

    public void LogFileDeleted(string sessionId, string filePath, long fileSize, bool isPermanent, bool isDryRun)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Deletion"))
        {
            var action = isDryRun ? "삭제 예정" : (isPermanent ? "영구 삭제됨" : "휴지통으로 이동됨");
            _logger.LogInformation(
                "파일 {Action} - 경로: {FilePath}, 크기: {FileSize:N0} bytes, 드라이런: {IsDryRun}",
                action, filePath, fileSize, isDryRun);
        }
    }

    public void LogDeletionCompleted(string sessionId, int successCount, int failedCount, long freedSpace, TimeSpan elapsed)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Deletion"))
        {
            _logger.LogInformation(
                "삭제 작업 완료 - 세션: {SessionId}, 성공: {SuccessCount}, 실패: {FailedCount}, " +
                "확보된 공간: {FreedSpace:N0} bytes ({FreedSpaceMB:F2} MB), 소요 시간: {Elapsed}",
                sessionId, successCount, failedCount, freedSpace,
                freedSpace / (1024.0 * 1024.0), elapsed);
        }
    }

    public void LogDeletionError(string sessionId, string filePath, Exception exception)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Deletion"))
        {
            _logger.LogError(exception,
                "파일 삭제 오류 - 세션: {SessionId}, 경로: {FilePath}",
                sessionId, filePath);
        }
    }

    public void LogDeletionBlocked(string sessionId, string filePath, string reason)
    {
        using (BeginScope("SessionId", sessionId))
        using (BeginScope("Operation", "Deletion"))
        {
            _logger.LogWarning(
                "삭제 차단됨 - 세션: {SessionId}, 경로: {FilePath}, 사유: {Reason}",
                sessionId, filePath, reason);
        }
    }

    #endregion

    #region 해시 관련 로깅

    public void LogHashComputed(string filePath, string hashType, string hashValue, TimeSpan elapsed)
    {
        if (_config.EnablePerformanceLogging)
        {
            _logger.LogDebug(
                "해시 계산 완료 - 파일: {FilePath}, 유형: {HashType}, 해시: {HashValue}, 소요 시간: {Elapsed:N0}ms",
                filePath, hashType, hashValue, elapsed.TotalMilliseconds);
        }
    }

    public void LogHashCacheHit(string filePath, string hashType)
    {
        if (_config.MinimumLevel <= Core.Logging.LogLevel.Debug)
        {
            _logger.LogDebug(
                "해시 캐시 히트 - 파일: {FilePath}, 유형: {HashType}",
                filePath, hashType);
        }
    }

    public void LogHashError(string filePath, string hashType, Exception exception)
    {
        _logger.LogError(exception,
            "해시 계산 오류 - 파일: {FilePath}, 유형: {HashType}",
            filePath, hashType);
    }

    #endregion

    #region 성능 관련 로깅

    public void LogPerformanceMetric(string operation, TimeSpan elapsed, IDictionary<string, object>? additionalData = null)
    {
        if (!_config.EnablePerformanceLogging) return;

        using (BeginScope("Operation", operation))
        {
            if (additionalData != null && additionalData.Count > 0)
            {
                _logger.LogInformation(
                    "성능 메트릭 - 작업: {Operation}, 소요 시간: {ElapsedMs:N2}ms, 추가 데이터: {@AdditionalData}",
                    operation, elapsed.TotalMilliseconds, additionalData);
            }
            else
            {
                _logger.LogInformation(
                    "성능 메트릭 - 작업: {Operation}, 소요 시간: {ElapsedMs:N2}ms",
                    operation, elapsed.TotalMilliseconds);
            }
        }
    }

    public void LogMemoryUsage(string context)
    {
        if (!_config.EnablePerformanceLogging) return;

        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64;
        var privateMemory = process.PrivateMemorySize64;
        var gcMemory = GC.GetTotalMemory(false);

        _logger.LogInformation(
            "메모리 사용량 - 컨텍스트: {Context}, 작업 세트: {WorkingSetMB:F2} MB, " +
            "전용 메모리: {PrivateMemoryMB:F2} MB, GC 힙: {GCMemoryMB:F2} MB",
            context,
            workingSet / (1024.0 * 1024.0),
            privateMemory / (1024.0 * 1024.0),
            gcMemory / (1024.0 * 1024.0));
    }

    #endregion

    #region 일반 로깅 메서드

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogWarning(Exception exception, string message, params object[] args)
    {
        _logger.LogWarning(exception, message, args);
    }

    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    public void LogCritical(string message, params object[] args)
    {
        _logger.LogCritical(message, args);
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        _logger.LogCritical(exception, message, args);
    }

    #endregion

    public IDisposable BeginScope(string name, object value)
    {
        return LogContext.PushProperty(name, value);
    }

    public IDisposable BeginScope(IDictionary<string, object> properties)
    {
        var disposables = new List<IDisposable>();
        foreach (var kvp in properties)
        {
            disposables.Add(LogContext.PushProperty(kvp.Key, kvp.Value));
        }
        return new CompositeDisposable(disposables);
    }

    private class CompositeDisposable : IDisposable
    {
        private readonly IEnumerable<IDisposable> _disposables;

        public CompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
