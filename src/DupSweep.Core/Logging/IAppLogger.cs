using Microsoft.Extensions.Logging;

namespace DupSweep.Core.Logging;

/// <summary>
/// DupSweep 애플리케이션 전용 로거 인터페이스.
/// Microsoft.Extensions.Logging.ILogger를 확장하여 도메인 특화 로깅 기능을 제공합니다.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// 기본 로거 인스턴스
    /// </summary>
    ILogger Logger { get; }

    #region 스캔 관련 로깅

    /// <summary>
    /// 스캔 시작 로그
    /// </summary>
    void LogScanStarted(string sessionId, IEnumerable<string> directories, string scanMode);

    /// <summary>
    /// 스캔 진행률 로그
    /// </summary>
    void LogScanProgress(string sessionId, int processedFiles, int totalFiles, string currentFile);

    /// <summary>
    /// 스캔 완료 로그
    /// </summary>
    void LogScanCompleted(string sessionId, int totalFiles, int duplicateGroups, long potentialSavings, TimeSpan elapsed);

    /// <summary>
    /// 스캔 취소 로그
    /// </summary>
    void LogScanCancelled(string sessionId, int processedFiles, TimeSpan elapsed);

    /// <summary>
    /// 스캔 오류 로그
    /// </summary>
    void LogScanError(string sessionId, Exception exception, string? context = null);

    #endregion

    #region 삭제 관련 로깅

    /// <summary>
    /// 삭제 작업 시작 로그
    /// </summary>
    void LogDeletionStarted(string sessionId, int fileCount, long totalSize, bool isPermanent, bool isDryRun);

    /// <summary>
    /// 개별 파일 삭제 로그
    /// </summary>
    void LogFileDeleted(string sessionId, string filePath, long fileSize, bool isPermanent, bool isDryRun);

    /// <summary>
    /// 삭제 작업 완료 로그
    /// </summary>
    void LogDeletionCompleted(string sessionId, int successCount, int failedCount, long freedSpace, TimeSpan elapsed);

    /// <summary>
    /// 삭제 오류 로그
    /// </summary>
    void LogDeletionError(string sessionId, string filePath, Exception exception);

    /// <summary>
    /// 삭제 차단됨 로그 (보호 폴더/확장자 등)
    /// </summary>
    void LogDeletionBlocked(string sessionId, string filePath, string reason);

    #endregion

    #region 해시 관련 로깅

    /// <summary>
    /// 해시 계산 완료 로그
    /// </summary>
    void LogHashComputed(string filePath, string hashType, string hashValue, TimeSpan elapsed);

    /// <summary>
    /// 해시 캐시 히트 로그
    /// </summary>
    void LogHashCacheHit(string filePath, string hashType);

    /// <summary>
    /// 해시 계산 오류 로그
    /// </summary>
    void LogHashError(string filePath, string hashType, Exception exception);

    #endregion

    #region 성능 관련 로깅

    /// <summary>
    /// 성능 메트릭 로그
    /// </summary>
    void LogPerformanceMetric(string operation, TimeSpan elapsed, IDictionary<string, object>? additionalData = null);

    /// <summary>
    /// 메모리 사용량 로그
    /// </summary>
    void LogMemoryUsage(string context);

    #endregion

    #region 일반 로깅 메서드

    void LogDebug(string message, params object[] args);
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogWarning(Exception exception, string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogCritical(string message, params object[] args);
    void LogCritical(Exception exception, string message, params object[] args);

    #endregion

    /// <summary>
    /// 컨텍스트 속성을 추가하여 새 로거 스코프를 생성합니다.
    /// </summary>
    IDisposable BeginScope(string name, object value);

    /// <summary>
    /// 여러 컨텍스트 속성을 추가하여 새 로거 스코프를 생성합니다.
    /// </summary>
    IDisposable BeginScope(IDictionary<string, object> properties);
}
