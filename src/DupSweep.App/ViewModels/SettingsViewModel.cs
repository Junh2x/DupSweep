using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 설정 화면 ViewModel
/// FFmpeg 경로, 병렬 처리 스레드 수 등 설정 관리
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _showConfirmationDialog = true;

    [ObservableProperty]
    private int _thumbnailSize = 128;

    [ObservableProperty]
    private int _parallelThreads;

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    public SettingsViewModel()
    {
        ParallelThreads = Environment.ProcessorCount;
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
}
