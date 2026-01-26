using System.Diagnostics;
using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Services.Interfaces;
using VBFileIO = Microsoft.VisualBasic.FileIO;

namespace DupSweep.Infrastructure.FileSystem;

/// <summary>
/// 파일 삭제 서비스 구현.
/// 안전한 삭제, 드라이런 모드, 삭제 로깅을 지원합니다.
/// </summary>
public class DeleteService : IDeleteService
{
    private readonly IAppLogger _logger;
    private readonly IDeleteValidationService _validationService;
    private bool _isDryRunMode;

    public event EventHandler<DeleteProgressEventArgs>? ProgressChanged;

    public DeleteService(IAppLogger logger, IDeleteValidationService validationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public IDeleteValidationService ValidationService => _validationService;

    public bool IsDryRunMode
    {
        get => _isDryRunMode;
        set
        {
            _isDryRunMode = value;
            _logger.LogInformation("드라이런 모드 {Status}", value ? "활성화" : "비활성화");
        }
    }

    #region 기본 삭제 메서드

    public async Task<DeleteOperationResult> MoveToTrashAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var sessionId = GenerateSessionId();
        return await SafeMoveToTrashAsync(filePaths, sessionId, cancellationToken);
    }

    public async Task<DeleteOperationResult> DeletePermanentlyAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var sessionId = GenerateSessionId();
        return await SafeDeletePermanentlyAsync(filePaths, sessionId, cancellationToken);
    }

    #endregion

    #region 안전 삭제 메서드

    public async Task<DeleteOperationResult> SafeMoveToTrashAsync(
        IEnumerable<string> filePaths,
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteDeleteAsync(filePaths, isPermanent: false, sessionId, cancellationToken);
    }

    public async Task<DeleteOperationResult> SafeDeletePermanentlyAsync(
        IEnumerable<string> filePaths,
        string sessionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteDeleteAsync(filePaths, isPermanent: true, sessionId, cancellationToken);
    }

    #endregion

    #region 드라이런

    public async Task<DeleteOperationResult> DryRunAsync(
        IEnumerable<string> filePaths,
        bool isPermanent,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var files = filePaths.ToList();
        var stopwatch = Stopwatch.StartNew();
        var result = new DeleteOperationResult
        {
            SessionId = sessionId,
            IsDryRun = true,
            IsPermanent = isPermanent,
            StartTime = DateTime.Now
        };

        _logger.LogDeletionStarted(sessionId, files.Count, 0, isPermanent, isDryRun: true);

        try
        {
            // 검증 수행
            var validation = await _validationService.ValidateAsync(files, cancellationToken);

            result.SkippedFiles = validation.BlockedFiles;
            result.SkippedCount = validation.BlockedFiles.Count;

            // 드라이런 - 실제 삭제 없이 시뮬레이션
            int processed = 0;
            foreach (var filePath in validation.AllowedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Exists)
                    {
                        var deletedInfo = new DeletedFileInfo
                        {
                            OriginalPath = filePath,
                            FileSize = fileInfo.Length,
                            DeletedAt = DateTime.Now
                        };

                        result.DeletedFiles.Add(deletedInfo);
                        result.FreedSpace += fileInfo.Length;
                        result.SuccessCount++;

                        _logger.LogFileDeleted(sessionId, filePath, fileInfo.Length, isPermanent, isDryRun: true);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add(new FailedFileInfo
                    {
                        FilePath = filePath,
                        Reason = "파일 정보 조회 실패",
                        ExceptionMessage = ex.Message
                    });
                    result.FailedCount++;
                }

                processed++;
                ReportProgress(processed, validation.AllowedFiles.Count, filePath, result.FreedSpace);
            }

            result.IsSuccess = true;
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "작업이 취소되었습니다.";
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "드라이런 중 오류 발생 - 세션: {SessionId}", sessionId);
        }

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        result.EndTime = DateTime.Now;

        _logger.LogDeletionCompleted(sessionId, result.SuccessCount, result.FailedCount, result.FreedSpace, result.Elapsed);

        return result;
    }

    #endregion

    #region 내부 구현

    private async Task<DeleteOperationResult> ExecuteDeleteAsync(
        IEnumerable<string> filePaths,
        bool isPermanent,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var files = filePaths.ToList();
        var stopwatch = Stopwatch.StartNew();
        var result = new DeleteOperationResult
        {
            SessionId = sessionId,
            IsDryRun = _isDryRunMode,
            IsPermanent = isPermanent,
            StartTime = DateTime.Now
        };

        // 드라이런 모드면 DryRunAsync로 위임
        if (_isDryRunMode)
        {
            return await DryRunAsync(files, isPermanent, sessionId, cancellationToken);
        }

        // 쿨다운 확인
        if (_validationService.IsCooldownActive)
        {
            var remaining = _validationService.RemainingCooldownMs;
            _logger.LogWarning("삭제 쿨다운 활성화됨 - 남은 시간: {RemainingMs}ms", remaining);
            await Task.Delay(remaining, cancellationToken);
        }

        // 초기 용량 계산
        long totalSize = 0;
        foreach (var file in files)
        {
            try
            {
                var fi = new FileInfo(file);
                if (fi.Exists) totalSize += fi.Length;
            }
            catch { }
        }

        _logger.LogDeletionStarted(sessionId, files.Count, totalSize, isPermanent, isDryRun: false);

        try
        {
            // 검증 수행
            var validation = await _validationService.ValidateAsync(files, cancellationToken);

            result.SkippedFiles = validation.BlockedFiles;
            result.SkippedCount = validation.BlockedFiles.Count;

            // 차단된 파일 로깅
            foreach (var blocked in validation.BlockedFiles)
            {
                _logger.LogDeletionBlocked(sessionId, blocked.FilePath, blocked.Message ?? blocked.Reason.ToString());
            }

            // 실제 삭제 수행
            int processed = 0;
            foreach (var filePath in validation.AllowedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    long fileSize = 0;

                    if (fileInfo.Exists)
                    {
                        fileSize = fileInfo.Length;

                        if (isPermanent)
                        {
                            File.Delete(filePath);
                        }
                        else
                        {
                            VBFileIO.FileSystem.DeleteFile(filePath, VBFileIO.UIOption.OnlyErrorDialogs, VBFileIO.RecycleOption.SendToRecycleBin);
                        }

                        var deletedInfo = new DeletedFileInfo
                        {
                            OriginalPath = filePath,
                            FileSize = fileSize,
                            DeletedAt = DateTime.Now
                        };

                        result.DeletedFiles.Add(deletedInfo);
                        result.FreedSpace += fileSize;
                        result.SuccessCount++;

                        _logger.LogFileDeleted(sessionId, filePath, fileSize, isPermanent, isDryRun: false);
                    }
                    else
                    {
                        result.FailedFiles.Add(new FailedFileInfo
                        {
                            FilePath = filePath,
                            Reason = "파일이 존재하지 않습니다."
                        });
                        result.FailedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add(new FailedFileInfo
                    {
                        FilePath = filePath,
                        Reason = "삭제 중 오류 발생",
                        ExceptionMessage = ex.Message
                    });
                    result.FailedCount++;

                    _logger.LogDeletionError(sessionId, filePath, ex);
                }

                processed++;
                ReportProgress(processed, validation.AllowedFiles.Count, filePath, result.FreedSpace);
            }

            result.IsSuccess = result.FailedCount == 0;

            // 쿨다운 시작
            _validationService.StartCooldown();
        }
        catch (OperationCanceledException)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "작업이 취소되었습니다.";
            _logger.LogWarning("삭제 작업 취소됨 - 세션: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "삭제 작업 중 오류 발생 - 세션: {SessionId}", sessionId);
        }

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        result.EndTime = DateTime.Now;

        _logger.LogDeletionCompleted(sessionId, result.SuccessCount, result.FailedCount, result.FreedSpace, result.Elapsed);

        return result;
    }

    private void ReportProgress(int processed, int total, string currentFile, long freedSoFar)
    {
        ProgressChanged?.Invoke(this, new DeleteProgressEventArgs
        {
            ProcessedCount = processed,
            TotalCount = total,
            CurrentFile = currentFile,
            FreedSoFar = freedSoFar
        });
    }

    private static string GenerateSessionId()
    {
        return $"DEL-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    #endregion
}
