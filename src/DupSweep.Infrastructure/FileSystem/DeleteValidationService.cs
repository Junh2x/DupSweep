using System.Text.RegularExpressions;
using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.Infrastructure.FileSystem;

/// <summary>
/// 파일 삭제 전 유효성 검증 서비스 구현.
/// 안전한 삭제를 위한 다양한 검증 로직을 제공합니다.
/// </summary>
public class DeleteValidationService : IDeleteValidationService
{
    private readonly IAppLogger _logger;
    private SafeDeleteOptions _options;
    private DateTime _lastDeletionTime = DateTime.MinValue;
    private readonly object _cooldownLock = new();

    public DeleteValidationService(IAppLogger logger, SafeDeleteOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? SafeDeleteOptions.Default;
    }

    public SafeDeleteOptions Options => _options;

    public void UpdateOptions(SafeDeleteOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger.LogInformation("삭제 검증 옵션이 업데이트되었습니다.");
    }

    public Task<DeleteValidationResult> ValidateAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ValidateInternal(filePaths, cancellationToken), cancellationToken);
    }

    private DeleteValidationResult ValidateInternal(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var files = filePaths.ToList();

        if (!files.Any())
        {
            return DeleteValidationResult.Success(Enumerable.Empty<string>(), 0);
        }

        var allowedFiles = new List<string>();
        var blockedFiles = new List<BlockedFile>();
        var warnings = new List<FileWarning>();
        long totalAllowedSize = 0;

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (canDelete, reason, message) = CanDeleteFile(filePath);

            if (canDelete)
            {
                allowedFiles.Add(filePath);

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists)
                    {
                        totalAllowedSize += fileInfo.Length;
                    }
                }
                catch
                {
                    // 파일 크기 조회 실패 시 무시
                }

                // 경고 수집
                var fileWarnings = GetWarnings(filePath);
                warnings.AddRange(fileWarnings);
            }
            else
            {
                blockedFiles.Add(new BlockedFile
                {
                    FilePath = filePath,
                    Reason = reason ?? BlockReason.Other,
                    Message = message
                });
            }
        }

        // 차단된 파일이 있으면 전체 거부
        if (blockedFiles.Any() && !_options.ProtectedExtensionWarningOnly)
        {
            _logger.LogWarning(
                "삭제 검증 실패 - 차단된 파일: {BlockedCount}/{TotalCount}",
                blockedFiles.Count, files.Count);

            return DeleteValidationResult.Denied(blockedFiles);
        }

        // 추가 확인이 필요한지 검사
        bool needsConfirmation = false;
        string? confirmationReason = null;

        // 파일 수 기준 확인
        if (allowedFiles.Count >= _options.DoubleConfirmThreshold)
        {
            needsConfirmation = true;
            confirmationReason = $"{allowedFiles.Count}개의 파일을 삭제하려고 합니다.";
        }

        // 용량 기준 확인
        if (totalAllowedSize >= _options.DoubleConfirmSizeThreshold)
        {
            needsConfirmation = true;
            var sizeMB = totalAllowedSize / (1024.0 * 1024.0);
            confirmationReason = confirmationReason != null
                ? $"{confirmationReason} 총 {sizeMB:F2} MB"
                : $"총 {sizeMB:F2} MB를 삭제하려고 합니다.";
        }

        // 보호된 확장자 경고 (경고만 모드)
        if (_options.ProtectedExtensionWarningOnly &&
            blockedFiles.Any(b => b.Reason == BlockReason.ProtectedExtension))
        {
            needsConfirmation = true;
            var protectedCount = blockedFiles.Count(b => b.Reason == BlockReason.ProtectedExtension);
            confirmationReason = confirmationReason != null
                ? $"{confirmationReason} (보호된 확장자 {protectedCount}개 포함)"
                : $"보호된 확장자를 가진 파일 {protectedCount}개가 포함되어 있습니다.";

            // 경고만 모드에서는 보호된 확장자 파일도 허용
            allowedFiles.AddRange(blockedFiles
                .Where(b => b.Reason == BlockReason.ProtectedExtension)
                .Select(b => b.FilePath));
            blockedFiles.RemoveAll(b => b.Reason == BlockReason.ProtectedExtension);
        }

        var result = needsConfirmation
            ? DeleteValidationResult.NeedsConfirmation(allowedFiles, totalAllowedSize, confirmationReason!)
            : DeleteValidationResult.Success(allowedFiles, totalAllowedSize);

        result.Warnings = warnings;
        result.BlockedFiles = blockedFiles;

        _logger.LogInformation(
            "삭제 검증 완료 - 허용: {AllowedCount}, 차단: {BlockedCount}, 경고: {WarningCount}, 확인 필요: {NeedsConfirmation}",
            allowedFiles.Count, blockedFiles.Count, warnings.Count, needsConfirmation);

        return result;
    }

    public (bool CanDelete, BlockReason? Reason, string? Message) CanDeleteFile(string filePath)
    {
        // 1. 파일 존재 확인
        if (_options.VerifyFileExistsBeforeDelete && !File.Exists(filePath))
        {
            return (false, BlockReason.FileNotFound, "파일이 존재하지 않습니다.");
        }

        // 2. 보호된 폴더 확인
        if (IsInProtectedFolder(filePath))
        {
            return (false, BlockReason.ProtectedFolder, "보호된 폴더에 있는 파일입니다.");
        }

        // 3. 보호된 확장자 확인
        if (HasProtectedExtension(filePath))
        {
            if (!_options.ProtectedExtensionWarningOnly)
            {
                return (false, BlockReason.ProtectedExtension, "보호된 확장자를 가진 파일입니다.");
            }
            // 경고만 모드에서는 허용하되 나중에 경고 표시
        }

        // 4. 파일 속성 확인
        try
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
            {
                return (false, BlockReason.FileNotFound, "파일이 존재하지 않습니다.");
            }

            // 시스템 파일 확인
            if (_options.BlockSystemFileDeletion &&
                (fileInfo.Attributes & FileAttributes.System) == FileAttributes.System)
            {
                return (false, BlockReason.SystemFile, "시스템 파일은 삭제할 수 없습니다.");
            }

            // 읽기 전용 파일 확인
            if (!_options.AllowReadOnlyFileDeletion &&
                (fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                return (false, BlockReason.ReadOnlyFile, "읽기 전용 파일입니다.");
            }
        }
        catch (UnauthorizedAccessException)
        {
            return (false, BlockReason.AccessDenied, "파일에 대한 접근 권한이 없습니다.");
        }
        catch (IOException ex) when (ex.HResult == -2147024864) // 파일이 사용 중
        {
            return (false, BlockReason.FileInUse, "파일이 다른 프로세스에서 사용 중입니다.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "파일 검증 중 오류 발생: {FilePath}", filePath);
            return (false, BlockReason.Other, $"파일 검증 중 오류: {ex.Message}");
        }

        return (true, null, null);
    }

    public bool IsInProtectedFolder(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath).ToLowerInvariant();

        // 정확한 경로 매칭
        foreach (var protectedFolder in _options.ProtectedFolders)
        {
            if (string.IsNullOrWhiteSpace(protectedFolder)) continue;

            var normalizedProtected = Path.GetFullPath(protectedFolder).ToLowerInvariant();
            if (normalizedPath.StartsWith(normalizedProtected + Path.DirectorySeparatorChar) ||
                normalizedPath.StartsWith(normalizedProtected + Path.AltDirectorySeparatorChar))
            {
                return true;
            }
        }

        // 패턴 매칭
        foreach (var pattern in _options.ProtectedFolderPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern)) continue;

            try
            {
                var regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*\\*", ".*")
                    .Replace("\\*", "[^\\\\]*")
                    .Replace("\\?", ".") + "$";

                if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // 패턴 매칭 타임아웃 - 무시
            }
        }

        return false;
    }

    public bool HasProtectedExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension)) return false;

        return _options.ProtectedExtensions
            .Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public List<FileWarning> GetWarnings(string filePath)
    {
        var warnings = new List<FileWarning>();

        try
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists) return warnings;

            // 숨김 파일 경고
            if (_options.WarnOnHiddenFileDeletion &&
                (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                warnings.Add(new FileWarning
                {
                    FilePath = filePath,
                    Type = WarningType.HiddenFile,
                    Message = "숨김 파일입니다."
                });
            }

            // 보호된 확장자 경고 (경고만 모드)
            if (_options.ProtectedExtensionWarningOnly && HasProtectedExtension(filePath))
            {
                warnings.Add(new FileWarning
                {
                    FilePath = filePath,
                    Type = WarningType.ProtectedExtension,
                    Message = $"보호된 확장자({Path.GetExtension(filePath)})를 가진 파일입니다."
                });
            }

            // 대용량 파일 경고 (100MB 이상)
            if (fileInfo.Length > 100 * 1024 * 1024)
            {
                warnings.Add(new FileWarning
                {
                    FilePath = filePath,
                    Type = WarningType.LargeFile,
                    Message = $"대용량 파일입니다. ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)"
                });
            }

            // 최근 수정 파일 경고 (24시간 이내)
            if ((DateTime.Now - fileInfo.LastWriteTime).TotalHours < 24)
            {
                warnings.Add(new FileWarning
                {
                    FilePath = filePath,
                    Type = WarningType.RecentlyModified,
                    Message = $"최근에 수정된 파일입니다. (수정: {fileInfo.LastWriteTime:g})"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("파일 경고 확인 중 오류: {FilePath}, 오류: {Error}", filePath, ex.Message);
        }

        return warnings;
    }

    public void AddProtectedFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        var normalizedPath = Path.GetFullPath(folderPath);
        if (!_options.ProtectedFolders.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
        {
            _options.ProtectedFolders.Add(normalizedPath);
            _logger.LogInformation("보호 폴더 추가: {FolderPath}", normalizedPath);
        }
    }

    public void RemoveProtectedFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        var normalizedPath = Path.GetFullPath(folderPath);
        var removed = _options.ProtectedFolders.RemoveAll(
            p => p.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            _logger.LogInformation("보호 폴더 제거: {FolderPath}", normalizedPath);
        }
    }

    public void AddProtectedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return;

        var normalizedExt = extension.StartsWith(".") ? extension : "." + extension;
        if (!_options.ProtectedExtensions.Contains(normalizedExt, StringComparer.OrdinalIgnoreCase))
        {
            _options.ProtectedExtensions.Add(normalizedExt);
            _logger.LogInformation("보호 확장자 추가: {Extension}", normalizedExt);
        }
    }

    public void RemoveProtectedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return;

        var normalizedExt = extension.StartsWith(".") ? extension : "." + extension;
        var removed = _options.ProtectedExtensions.RemoveAll(
            e => e.Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            _logger.LogInformation("보호 확장자 제거: {Extension}", normalizedExt);
        }
    }

    public bool IsCooldownActive
    {
        get
        {
            if (!_options.EnableDeletionCooldown) return false;

            lock (_cooldownLock)
            {
                return RemainingCooldownMs > 0;
            }
        }
    }

    public int RemainingCooldownMs
    {
        get
        {
            if (!_options.EnableDeletionCooldown) return 0;

            lock (_cooldownLock)
            {
                var elapsed = (DateTime.Now - _lastDeletionTime).TotalMilliseconds;
                var remaining = _options.DeletionCooldownMs - elapsed;
                return remaining > 0 ? (int)remaining : 0;
            }
        }
    }

    public void StartCooldown()
    {
        if (!_options.EnableDeletionCooldown) return;

        lock (_cooldownLock)
        {
            _lastDeletionTime = DateTime.Now;
            _logger.LogDebug("삭제 쿨다운 시작: {CooldownMs}ms", _options.DeletionCooldownMs);
        }
    }

    public void ResetCooldown()
    {
        lock (_cooldownLock)
        {
            _lastDeletionTime = DateTime.MinValue;
            _logger.LogDebug("삭제 쿨다운 리셋");
        }
    }
}
