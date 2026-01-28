using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 설정 화면 ViewModel
/// 테마, FFmpeg 경로, 병렬 처리 스레드 수 등 설정 관리
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _moveToTrashByDefault = true;

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
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(IsDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
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
        IsDarkTheme = false;
        MoveToTrashByDefault = true;
        ShowConfirmationDialog = true;
        ThumbnailSize = 128;
        ParallelThreads = Environment.ProcessorCount;
        FfmpegPath = string.Empty;
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(BaseTheme.Light);
        paletteHelper.SetTheme(theme);
    }
}
