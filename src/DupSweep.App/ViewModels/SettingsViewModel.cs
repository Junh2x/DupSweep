using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.App.Services;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 설정 화면 ViewModel
/// FFmpeg 경로, 병렬 처리 스레드 수, 언어 등 설정 관리
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

    /// <summary>
    /// 사용 가능한 언어 목록
    /// </summary>
    public List<LanguageOption> AvailableLanguages { get; } =
    [
        new(AppLanguage.Korean, "한국어"),
        new(AppLanguage.English, "English")
    ];

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    public SettingsViewModel()
    {
        ParallelThreads = Environment.ProcessorCount;
        _selectedLanguage = AvailableLanguages.First(
            l => l.Language == LanguageService.Instance.CurrentLanguage);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        LanguageService.Instance.SetLanguage(value.Language);
    }

    [RelayCommand]
    private void BrowseFFmpegPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LanguageService.Instance.GetString("Settings.SelectFFmpeg"),
            Filter = LanguageService.Instance.GetString("Settings.FFmpegFilter")
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
        SelectedLanguage = AvailableLanguages.First(l => l.Language == AppLanguage.Korean);
    }
}

/// <summary>
/// 언어 선택 옵션
/// </summary>
public record LanguageOption(AppLanguage Language, string DisplayName);
