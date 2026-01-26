using DupSweep.Core.Models;

namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 파일 삭제 서비스 인터페이스.
/// 안전한 삭제, 드라이런, 로깅 기능을 제공합니다.
/// </summary>
public interface IDeleteService
{
    /// <summary>
    /// 파일을 휴지통으로 이동합니다.
    /// </summary>
    Task<DeleteOperationResult> MoveToTrashAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// 파일을 영구 삭제합니다.
    /// </summary>
    Task<DeleteOperationResult> DeletePermanentlyAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken);

    /// <summary>
    /// 안전 옵션을 적용하여 파일을 휴지통으로 이동합니다.
    /// </summary>
    Task<DeleteOperationResult> SafeMoveToTrashAsync(
        IEnumerable<string> filePaths,
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 안전 옵션을 적용하여 파일을 영구 삭제합니다.
    /// </summary>
    Task<DeleteOperationResult> SafeDeletePermanentlyAsync(
        IEnumerable<string> filePaths,
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 드라이런 모드로 삭제 시뮬레이션을 수행합니다.
    /// 실제 삭제는 수행하지 않고 결과만 반환합니다.
    /// </summary>
    Task<DeleteOperationResult> DryRunAsync(
        IEnumerable<string> filePaths,
        bool isPermanent,
        string sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 삭제 검증 서비스
    /// </summary>
    IDeleteValidationService ValidationService { get; }

    /// <summary>
    /// 드라이런 모드 활성화 여부
    /// </summary>
    bool IsDryRunMode { get; set; }

    /// <summary>
    /// 삭제 진행률 이벤트
    /// </summary>
    event EventHandler<DeleteProgressEventArgs>? ProgressChanged;
}

/// <summary>
/// 삭제 작업 결과
/// </summary>
public class DeleteOperationResult
{
    /// <summary>
    /// 세션 ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 작업 성공 여부
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 드라이런 모드 여부
    /// </summary>
    public bool IsDryRun { get; set; }

    /// <summary>
    /// 영구 삭제 여부
    /// </summary>
    public bool IsPermanent { get; set; }

    /// <summary>
    /// 성공적으로 삭제된 파일 수
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 삭제 실패한 파일 수
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 건너뛴 파일 수 (보호됨 등)
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 확보된 공간 (바이트)
    /// </summary>
    public long FreedSpace { get; set; }

    /// <summary>
    /// 작업 소요 시간
    /// </summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>
    /// 삭제된 파일 목록
    /// </summary>
    public List<DeletedFileInfo> DeletedFiles { get; set; } = new();

    /// <summary>
    /// 실패한 파일 목록
    /// </summary>
    public List<FailedFileInfo> FailedFiles { get; set; } = new();

    /// <summary>
    /// 건너뛴 파일 목록 (보호됨)
    /// </summary>
    public List<BlockedFile> SkippedFiles { get; set; } = new();

    /// <summary>
    /// 오류 메시지
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 작업 시작 시간
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 작업 종료 시간
    /// </summary>
    public DateTime EndTime { get; set; }
}

/// <summary>
/// 삭제된 파일 정보
/// </summary>
public class DeletedFileInfo
{
    /// <summary>
    /// 원본 파일 경로
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// 파일 크기
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 파일 해시 (있는 경우)
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// 삭제 시간
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// 휴지통 위치 (휴지통 이동 시)
    /// </summary>
    public string? TrashLocation { get; set; }
}

/// <summary>
/// 삭제 실패 파일 정보
/// </summary>
public class FailedFileInfo
{
    /// <summary>
    /// 파일 경로
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 실패 사유
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 예외 정보
    /// </summary>
    public string? ExceptionMessage { get; set; }
}

/// <summary>
/// 삭제 진행률 이벤트 인자
/// </summary>
public class DeleteProgressEventArgs : EventArgs
{
    /// <summary>
    /// 처리된 파일 수
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// 전체 파일 수
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 현재 파일 경로
    /// </summary>
    public string CurrentFile { get; set; } = string.Empty;

    /// <summary>
    /// 진행률 (0-100)
    /// </summary>
    public int ProgressPercentage => TotalCount > 0 ? (ProcessedCount * 100 / TotalCount) : 0;

    /// <summary>
    /// 현재까지 확보된 공간
    /// </summary>
    public long FreedSoFar { get; set; }
}
