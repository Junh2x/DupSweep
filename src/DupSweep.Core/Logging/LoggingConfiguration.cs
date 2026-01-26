namespace DupSweep.Core.Logging;

/// <summary>
/// 로깅 시스템 구성 옵션
/// </summary>
public class LoggingConfiguration
{
    /// <summary>
    /// 로그 파일 저장 디렉토리
    /// </summary>
    public string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DupSweep", "Logs");

    /// <summary>
    /// 최소 로그 레벨
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 파일 로깅 활성화 여부
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// 콘솔 로깅 활성화 여부 (디버그 모드에서만 사용)
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = false;

    /// <summary>
    /// 로그 파일 보관 일수
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 30;

    /// <summary>
    /// 개별 로그 파일 최대 크기 (바이트)
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// 롤링 인터벌
    /// </summary>
    public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;

    /// <summary>
    /// 삭제 작업 전용 로그 파일 활성화
    /// </summary>
    public bool EnableDeletionLog { get; set; } = true;

    /// <summary>
    /// 삭제 로그 파일명 패턴
    /// </summary>
    public string DeletionLogFilePattern { get; set; } = "deletion-.log";

    /// <summary>
    /// 성능 로그 활성화 여부
    /// </summary>
    public bool EnablePerformanceLogging { get; set; } = true;

    /// <summary>
    /// 스레드 정보 포함 여부
    /// </summary>
    public bool IncludeThreadInfo { get; set; } = true;

    /// <summary>
    /// 머신 정보 포함 여부
    /// </summary>
    public bool IncludeMachineInfo { get; set; } = false;

    /// <summary>
    /// 출력 템플릿 (파일용)
    /// </summary>
    public string FileOutputTemplate { get; set; } =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 출력 템플릿 (콘솔용)
    /// </summary>
    public string ConsoleOutputTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// JSON 형식 로그 활성화 여부
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// 구조화된 로그 파일명 패턴
    /// </summary>
    public string StructuredLogFilePattern { get; set; } = "dupsweep-.json";

    /// <summary>
    /// 일반 텍스트 로그 파일명 패턴
    /// </summary>
    public string TextLogFilePattern { get; set; } = "dupsweep-.log";
}

/// <summary>
/// 로그 레벨 정의
/// </summary>
public enum LogLevel
{
    Verbose = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}

/// <summary>
/// 로그 파일 롤링 인터벌
/// </summary>
public enum RollingInterval
{
    Infinite = 0,
    Year = 1,
    Month = 2,
    Day = 3,
    Hour = 4,
    Minute = 5
}
