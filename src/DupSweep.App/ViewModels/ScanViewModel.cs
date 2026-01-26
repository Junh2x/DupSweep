using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

public partial class ScanViewModel : ObservableObject
{
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
    private string _statusMessage = "Ready to scan";

    public string FormattedPotentialSavings => FormatFileSize(PotentialSavings);

    public ScanViewModel()
    {
    }

    [RelayCommand]
    private void PauseScan()
    {
        var scanService = App.Services.GetService(typeof(DupSweep.Core.Services.Interfaces.IScanService)) as DupSweep.Core.Services.Interfaces.IScanService;
        if (scanService == null || !scanService.IsRunning)
        {
            return;
        }

        if (scanService.IsPaused)
        {
            scanService.Resume();
        }
        else
        {
            scanService.Pause();
        }

        IsPaused = scanService.IsPaused;
        StatusMessage = IsPaused ? "Paused" : "Scanning...";
    }

    [RelayCommand]
    private void CancelScan()
    {
        var scanService = App.Services.GetService(typeof(DupSweep.Core.Services.Interfaces.IScanService)) as DupSweep.Core.Services.Interfaces.IScanService;
        if (scanService == null || !scanService.IsRunning)
        {
            return;
        }

        scanService.Cancel();
        StatusMessage = "Scan cancelled";
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
        StatusMessage = "Scanning...";
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
