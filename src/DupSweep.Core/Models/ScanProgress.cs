namespace DupSweep.Core.Models;

/// <summary>
/// 스캔 진행 상황 모델 클래스
/// 현재 단계, 처리된 파일 수, 경과 시간 등 진행 정보 포함
/// </summary>
public class ScanProgress
{
    public ScanPhase Phase { get; set; }              // 현재 스캔 단계
    public int TotalFiles { get; set; }               // 전체 파일 수
    public int ProcessedFiles { get; set; }           // 처리된 파일 수
    public string CurrentFile { get; set; } = string.Empty; // 현재 처리 중인 파일
    public int DuplicateGroupsFound { get; set; }     // 발견된 중복 그룹 수
    public long PotentialSavings { get; set; }        // 절약 가능 용량
    public TimeSpan ElapsedTime { get; set; }         // 경과 시간
    public bool IsPaused { get; set; }                // 일시 정지 상태
    public bool IsCancelled { get; set; }             // 취소 상태

    /// <summary>
    /// 진행률 백분율 (0-100)
    /// </summary>
    public double ProgressPercentage => TotalFiles > 0
        ? (double)ProcessedFiles / TotalFiles * 100
        : 0;

    /// <summary>
    /// 현재 단계에 맞는 상태 메시지 반환
    /// </summary>
    /// <summary>
    /// 현재 단계와 처리 상태를 나타내는 키-기반 상태 메시지.
    /// UI 레이어에서 LanguageService를 통해 번역된 문자열로 변환합니다.
    /// </summary>
    public string StatusMessage => Phase switch
    {
        ScanPhase.Initializing => "ScanProgress.Initializing",
        ScanPhase.Scanning => $"ScanProgress.Scanning|{ProcessedFiles}|{TotalFiles}",
        ScanPhase.Hashing => $"ScanProgress.Hashing|{ProcessedFiles}|{TotalFiles}",
        ScanPhase.Comparing => "ScanProgress.Comparing",
        ScanPhase.Completed => "ScanProgress.Completed",
        ScanPhase.Cancelled => "ScanProgress.Cancelled",
        ScanPhase.Error => "ScanProgress.Error",
        _ => "ScanProgress.Unknown"
    };
}

/// <summary>
/// 스캔 단계 열거형
/// </summary>
public enum ScanPhase
{
    Initializing,         // 초기화
    Scanning,             // 파일 탐색
    Hashing,              // 해시 계산
    Comparing,            // 중복 비교
    Completed,            // 완료
    Cancelled,            // 취소됨
    Error                 // 오류
}
