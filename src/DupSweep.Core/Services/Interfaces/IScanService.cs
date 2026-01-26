using DupSweep.Core.Models;

namespace DupSweep.Core.Services.Interfaces;

public interface IScanService
{
    bool IsRunning { get; }
    bool IsPaused { get; }

    Task<ScanResult> StartScanAsync(ScanConfig config, IProgress<ScanProgress> progress);
    void Pause();
    void Resume();
    void Cancel();
}
