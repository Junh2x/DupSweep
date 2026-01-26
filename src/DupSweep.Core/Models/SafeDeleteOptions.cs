namespace DupSweep.Core.Models;

/// <summary>
/// 안전한 삭제 작업을 위한 설정 옵션.
/// 실수로 인한 데이터 손실을 방지하기 위한 다양한 안전장치를 제공합니다.
/// </summary>
public class SafeDeleteOptions
{
    /// <summary>
    /// 이중 확인이 필요한 최소 파일 수.
    /// 이 수 이상의 파일을 삭제할 때 추가 확인을 요청합니다.
    /// </summary>
    public int DoubleConfirmThreshold { get; set; } = 10;

    /// <summary>
    /// 이중 확인이 필요한 최소 용량 (바이트).
    /// 이 용량 이상을 삭제할 때 추가 확인을 요청합니다.
    /// </summary>
    public long DoubleConfirmSizeThreshold { get; set; } = 1024 * 1024 * 100; // 100MB

    /// <summary>
    /// 보호된 폴더 경로 목록.
    /// 이 폴더 내의 파일은 삭제가 차단됩니다.
    /// </summary>
    public List<string> ProtectedFolders { get; set; } = new()
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)
    };

    /// <summary>
    /// 보호된 파일 확장자 목록.
    /// 이 확장자를 가진 파일 삭제 시 경고를 표시합니다.
    /// </summary>
    public List<string> ProtectedExtensions { get; set; } = new()
    {
        ".exe", ".dll", ".sys", ".drv",  // 시스템/실행 파일
        ".psd", ".ai", ".indd",           // 전문 디자인 파일
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",  // Office 문서
        ".pdf",                           // PDF 문서
        ".db", ".sqlite", ".mdb",         // 데이터베이스
        ".key", ".pem", ".pfx", ".cer"    // 인증서/키
    };

    /// <summary>
    /// 삭제 작업 간 쿨다운 시간 (밀리초).
    /// 연속적인 삭제를 방지합니다.
    /// </summary>
    public int DeletionCooldownMs { get; set; } = 3000;

    /// <summary>
    /// 삭제 쿨다운 활성화 여부.
    /// </summary>
    public bool EnableDeletionCooldown { get; set; } = true;

    /// <summary>
    /// 보호된 확장자 삭제 시 경고만 표시 (true) 또는 완전 차단 (false).
    /// </summary>
    public bool ProtectedExtensionWarningOnly { get; set; } = true;

    /// <summary>
    /// 보호된 폴더 경로 패턴 (와일드카드/정규식 지원).
    /// </summary>
    public List<string> ProtectedFolderPatterns { get; set; } = new()
    {
        "*\\Windows\\*",
        "*\\Program Files\\*",
        "*\\Program Files (x86)\\*",
        "*\\$Recycle.Bin\\*"
    };

    /// <summary>
    /// 읽기 전용 파일 삭제 허용 여부.
    /// </summary>
    public bool AllowReadOnlyFileDeletion { get; set; } = false;

    /// <summary>
    /// 숨김 파일 삭제 시 경고 표시 여부.
    /// </summary>
    public bool WarnOnHiddenFileDeletion { get; set; } = true;

    /// <summary>
    /// 시스템 파일 삭제 차단 여부.
    /// </summary>
    public bool BlockSystemFileDeletion { get; set; } = true;

    /// <summary>
    /// 삭제 전 파일 존재 여부 재확인.
    /// </summary>
    public bool VerifyFileExistsBeforeDelete { get; set; } = true;

    /// <summary>
    /// 최대 동시 삭제 파일 수.
    /// 0은 무제한을 의미합니다.
    /// </summary>
    public int MaxConcurrentDeletions { get; set; } = 100;

    /// <summary>
    /// 기본 설정을 반환합니다.
    /// </summary>
    public static SafeDeleteOptions Default => new();

    /// <summary>
    /// 엄격한 보안 설정을 반환합니다.
    /// </summary>
    public static SafeDeleteOptions Strict => new()
    {
        DoubleConfirmThreshold = 5,
        DoubleConfirmSizeThreshold = 50 * 1024 * 1024, // 50MB
        EnableDeletionCooldown = true,
        DeletionCooldownMs = 5000,
        ProtectedExtensionWarningOnly = false,
        AllowReadOnlyFileDeletion = false,
        WarnOnHiddenFileDeletion = true,
        BlockSystemFileDeletion = true
    };

    /// <summary>
    /// 최소 보안 설정을 반환합니다 (고급 사용자용).
    /// </summary>
    public static SafeDeleteOptions Minimal => new()
    {
        DoubleConfirmThreshold = 50,
        DoubleConfirmSizeThreshold = 1024 * 1024 * 1024, // 1GB
        EnableDeletionCooldown = false,
        ProtectedExtensionWarningOnly = true,
        AllowReadOnlyFileDeletion = true,
        WarnOnHiddenFileDeletion = false,
        BlockSystemFileDeletion = true
    };
}

/// <summary>
/// 삭제 검증 결과
/// </summary>
public class DeleteValidationResult
{
    /// <summary>
    /// 삭제가 허용되는지 여부
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// 추가 확인이 필요한지 여부
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// 확인이 필요한 사유
    /// </summary>
    public string? ConfirmationReason { get; set; }

    /// <summary>
    /// 차단된 파일 목록
    /// </summary>
    public List<BlockedFile> BlockedFiles { get; set; } = new();

    /// <summary>
    /// 경고가 있는 파일 목록
    /// </summary>
    public List<FileWarning> Warnings { get; set; } = new();

    /// <summary>
    /// 삭제 가능한 파일 목록
    /// </summary>
    public List<string> AllowedFiles { get; set; } = new();

    /// <summary>
    /// 삭제 가능한 총 용량
    /// </summary>
    public long AllowedTotalSize { get; set; }

    /// <summary>
    /// 검증 성공 결과를 생성합니다.
    /// </summary>
    public static DeleteValidationResult Success(IEnumerable<string> files, long totalSize)
    {
        return new DeleteValidationResult
        {
            IsAllowed = true,
            RequiresConfirmation = false,
            AllowedFiles = files.ToList(),
            AllowedTotalSize = totalSize
        };
    }

    /// <summary>
    /// 확인 필요 결과를 생성합니다.
    /// </summary>
    public static DeleteValidationResult NeedsConfirmation(
        IEnumerable<string> files,
        long totalSize,
        string reason)
    {
        return new DeleteValidationResult
        {
            IsAllowed = true,
            RequiresConfirmation = true,
            ConfirmationReason = reason,
            AllowedFiles = files.ToList(),
            AllowedTotalSize = totalSize
        };
    }

    /// <summary>
    /// 거부 결과를 생성합니다.
    /// </summary>
    public static DeleteValidationResult Denied(IEnumerable<BlockedFile> blockedFiles)
    {
        return new DeleteValidationResult
        {
            IsAllowed = false,
            RequiresConfirmation = false,
            BlockedFiles = blockedFiles.ToList()
        };
    }
}

/// <summary>
/// 차단된 파일 정보
/// </summary>
public class BlockedFile
{
    /// <summary>
    /// 파일 경로
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 차단 사유
    /// </summary>
    public BlockReason Reason { get; set; }

    /// <summary>
    /// 상세 메시지
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 파일 경고 정보
/// </summary>
public class FileWarning
{
    /// <summary>
    /// 파일 경로
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 경고 유형
    /// </summary>
    public WarningType Type { get; set; }

    /// <summary>
    /// 경고 메시지
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 차단 사유 열거형
/// </summary>
public enum BlockReason
{
    /// <summary>
    /// 보호된 폴더에 위치
    /// </summary>
    ProtectedFolder,

    /// <summary>
    /// 보호된 확장자
    /// </summary>
    ProtectedExtension,

    /// <summary>
    /// 시스템 파일
    /// </summary>
    SystemFile,

    /// <summary>
    /// 읽기 전용 파일
    /// </summary>
    ReadOnlyFile,

    /// <summary>
    /// 파일이 존재하지 않음
    /// </summary>
    FileNotFound,

    /// <summary>
    /// 접근 권한 없음
    /// </summary>
    AccessDenied,

    /// <summary>
    /// 파일이 사용 중
    /// </summary>
    FileInUse,

    /// <summary>
    /// 기타 사유
    /// </summary>
    Other
}

/// <summary>
/// 경고 유형 열거형
/// </summary>
public enum WarningType
{
    /// <summary>
    /// 숨김 파일
    /// </summary>
    HiddenFile,

    /// <summary>
    /// 보호된 확장자 (경고만)
    /// </summary>
    ProtectedExtension,

    /// <summary>
    /// 대용량 파일
    /// </summary>
    LargeFile,

    /// <summary>
    /// 최근 수정된 파일
    /// </summary>
    RecentlyModified,

    /// <summary>
    /// 기타 경고
    /// </summary>
    Other
}
