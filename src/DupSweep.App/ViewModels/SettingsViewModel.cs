using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 설정 화면 ViewModel
/// FFmpeg 경로, 병렬 처리 스레드 수 등 설정 관리
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IThumbnailCache _thumbnailCache;

    [ObservableProperty]
    private bool _showConfirmationDialog = true;

    [ObservableProperty]
    private int _thumbnailSize = 128;

    [ObservableProperty]
    private int _parallelThreads;

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    [ObservableProperty]
    private string _cacheSize = "계산 중...";

    public SettingsViewModel(IThumbnailCache thumbnailCache)
    {
        _thumbnailCache = thumbnailCache;
        ParallelThreads = Environment.ProcessorCount;
        RefreshCacheSize();
    }

    [RelayCommand]
    private void BrowseFFmpegPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select FFmpeg executable",
            Filter = "Executable files (*.exe)|*.exe"
        };

        if (dialog.ShowDialog() == true)
        {
            FfmpegPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        ShowConfirmationDialog = true;
        ThumbnailSize = 128;
        ParallelThreads = Environment.ProcessorCount;
        FfmpegPath = string.Empty;
    }

    [RelayCommand]
    private void ClearCache()
    {
        _thumbnailCache.ClearCache();
        RefreshCacheSize();
    }

    public void RefreshCacheSize()
    {
        var size = _thumbnailCache.GetCacheSize();
        CacheSize = FormatFileSize(size);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
