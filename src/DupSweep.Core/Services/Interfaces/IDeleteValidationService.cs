using DupSweep.Core.Models;

namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 파일 삭제 전 유효성 검증을 수행하는 서비스 인터페이스.
/// 보호 폴더, 보호 확장자, 시스템 파일 등의 삭제를 방지합니다.
/// </summary>
public interface IDeleteValidationService
{
    /// <summary>
    /// 현재 사용 중인 삭제 옵션을 가져옵니다.
    /// </summary>
    SafeDeleteOptions Options { get; }

    /// <summary>
    /// 삭제 옵션을 업데이트합니다.
    /// </summary>
    void UpdateOptions(SafeDeleteOptions options);

    /// <summary>
    /// 파일 목록에 대한 삭제 유효성을 검증합니다.
    /// </summary>
    /// <param name="filePaths">검증할 파일 경로 목록</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>검증 결과</returns>
    Task<DeleteValidationResult> ValidateAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 단일 파일의 삭제 가능 여부를 확인합니다.
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>삭제 가능 여부와 차단 사유</returns>
    (bool CanDelete, BlockReason? Reason, string? Message) CanDeleteFile(string filePath);

    /// <summary>
    /// 파일이 보호된 폴더에 있는지 확인합니다.
    /// </summary>
    bool IsInProtectedFolder(string filePath);

    /// <summary>
    /// 파일이 보호된 확장자인지 확인합니다.
    /// </summary>
    bool HasProtectedExtension(string filePath);

    /// <summary>
    /// 파일에 대한 경고를 확인합니다.
    /// </summary>
    List<FileWarning> GetWarnings(string filePath);

    /// <summary>
    /// 보호 폴더를 추가합니다.
    /// </summary>
    void AddProtectedFolder(string folderPath);

    /// <summary>
    /// 보호 폴더를 제거합니다.
    /// </summary>
    void RemoveProtectedFolder(string folderPath);

    /// <summary>
    /// 보호 확장자를 추가합니다.
    /// </summary>
    void AddProtectedExtension(string extension);

    /// <summary>
    /// 보호 확장자를 제거합니다.
    /// </summary>
    void RemoveProtectedExtension(string extension);

    /// <summary>
    /// 삭제 쿨다운이 활성화되어 있고 대기 중인지 확인합니다.
    /// </summary>
    bool IsCooldownActive { get; }

    /// <summary>
    /// 남은 쿨다운 시간 (밀리초)
    /// </summary>
    int RemainingCooldownMs { get; }

    /// <summary>
    /// 쿨다운을 시작합니다.
    /// </summary>
    void StartCooldown();

    /// <summary>
    /// 쿨다운을 리셋합니다.
    /// </summary>
    void ResetCooldown();
}
