using DupSweep.Core.Models;

namespace DupSweep.Core.Services.Interfaces;

/// <summary>
/// 스캔 서비스 인터페이스
/// 중복 파일 스캔의 시작, 일시정지, 재개, 취소 기능 제공
/// </summary>
public interface IScanService
{
    /// <summary>스캔 실행 중 여부</summary>
    bool IsRunning { get; }

    /// <summary>스캔 일시정지 상태 여부</summary>
    bool IsPaused { get; }

    /// <summary>스캔 시작</summary>
    Task<ScanResult> StartScanAsync(ScanConfig config, IProgress<ScanProgress> progress);

    /// <summary>스캔 일시정지</summary>
    void Pause();

    /// <summary>스캔 재개</summary>
    void Resume();

    /// <summary>스캔 취소</summary>
    void Cancel();
}
