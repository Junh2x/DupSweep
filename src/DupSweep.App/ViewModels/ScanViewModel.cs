using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.App.Services;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 스캔 진행 화면 ViewModel
/// 스캔 진행률, 상태 메시지, 일시정지/취소 기능 제공
/// </summary>
public partial class ScanViewModel : ObservableObject
{
    private readonly IScanService _scanService;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _scannedFiles;

    [ObservableProperty]
    private int _duplicateGroups;

    [ObservableProperty]
    private long _potentialSavings;

    [ObservableProperty]
    private TimeSpan _elapsedTime;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string FormattedPotentialSavings => FormatFileSize(PotentialSavings);

    public ScanViewModel(IScanService scanService)
    {
        _scanService = scanService;
        _statusMessage = LanguageService.Instance.GetString("Scan.ReadyToScan");
    }

    [RelayCommand]
    private void PauseScan()
    {
        if (!_scanService.IsRunning)
        {
            return;
        }

        if (_scanService.IsPaused)
        {
            _scanService.Resume();
        }
        else
        {
            _scanService.Pause();
        }

        IsPaused = _scanService.IsPaused;
        StatusMessage = IsPaused
            ? LanguageService.Instance.GetString("Scan.Paused")
            : LanguageService.Instance.GetString("Scan.Scanning");
    }

    [RelayCommand]
    private void CancelScan()
    {
        if (!_scanService.IsRunning)
        {
            return;
        }

        _scanService.Cancel();
        StatusMessage = LanguageService.Instance.GetString("Scan.Cancelled");
    }

    public void Reset()
    {
        Progress = 0;
        CurrentFile = string.Empty;
        TotalFiles = 0;
        ScannedFiles = 0;
        DuplicateGroups = 0;
        PotentialSavings = 0;
        ElapsedTime = TimeSpan.Zero;
        IsScanning = true;
        IsPaused = false;
        StatusMessage = LanguageService.Instance.GetString("Scan.Scanning");
        OnPropertyChanged(nameof(FormattedPotentialSavings));
    }

    public void ApplyProgress(DupSweep.Core.Models.ScanProgress progress)
    {
        Progress = progress.ProgressPercentage;
        CurrentFile = progress.CurrentFile;
        TotalFiles = progress.TotalFiles;
        ScannedFiles = progress.ProcessedFiles;
        DuplicateGroups = progress.DuplicateGroupsFound;
        PotentialSavings = progress.PotentialSavings;
        ElapsedTime = progress.ElapsedTime;
        IsPaused = progress.IsPaused;
        IsScanning = progress.Phase is not DupSweep.Core.Models.ScanPhase.Completed
            and not DupSweep.Core.Models.ScanPhase.Cancelled
            and not DupSweep.Core.Models.ScanPhase.Error;
        StatusMessage = progress.StatusMessage;
        OnPropertyChanged(nameof(FormattedPotentialSavings));
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    partial void OnPotentialSavingsChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedPotentialSavings));
    }
}
