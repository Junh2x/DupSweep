using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DupSweep.App.Messages;
using DupSweep.App.Services;
using DupSweep.Core.Logging;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 홈 화면 ViewModel
/// 스캔 대상 폴더 선택 및 중복 감지 옵션 설정
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly ScanViewModel _scanViewModel;
    private readonly ResultsViewModel _resultsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IAppLogger _logger;
    private readonly IMessenger _messenger;

    /// <summary>
    /// 스캔할 폴더 목록
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _selectedFolders = new();

    // 감지 조건
    [ObservableProperty]
    private bool _useHashComparison;

    [ObservableProperty]
    private bool _useSizeComparison;

    [ObservableProperty]
    private bool _useResolutionComparison;

    [ObservableProperty]
    private bool _useImageSimilarity;

    [ObservableProperty]
    private bool _useVideoSimilarity;

    [ObservableProperty]
    private bool _matchCreatedDate;

    [ObservableProperty]
    private bool _matchModifiedDate;

    // 유사도 임계값
    [ObservableProperty]
    private double _similarityThreshold = 85;

    [ObservableProperty]
    private double _videoSimilarityThreshold = 85;

    // 파일 타입
    [ObservableProperty]
    private bool _scanAllFiles = true;  // 모든 파일 대상 (해시 기반)

    [ObservableProperty]
    private bool _scanImages = true;    // 이미지 유사도 비교

    [ObservableProperty]
    private bool _scanVideos = true;    // 비디오 유사도 비교

    [ObservableProperty]
    private bool _scanAudio;            // 오디오 스캔

    [ObservableProperty]
    private bool _scanDocuments;        // 문서 스캔

    [ObservableProperty]
    private bool _canStartScan;

    public HomeViewModel(
        IScanService scanService,
        ScanViewModel scanViewModel,
        ResultsViewModel resultsViewModel,
        SettingsViewModel settingsViewModel,
        IAppLogger logger,
        IMessenger messenger)
    {
        _scanService = scanService;
        _scanViewModel = scanViewModel;
        _resultsViewModel = resultsViewModel;
        _settingsViewModel = settingsViewModel;
        _logger = logger;
        _messenger = messenger;

        SelectedFolders.CollectionChanged += (_, _) => UpdateCanStartScan();
    }

    private void UpdateCanStartScan()
    {
        CanStartScan = SelectedFolders.Count > 0;
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Folder to Scan",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var folder in dialog.FolderNames)
            {
                AddFolder(folder);
            }
        }
    }

    /// <summary>
    /// 폴더를 추가합니다 (드래그 앤 드롭용).
    /// </summary>
    public void AddFolder(string folderPath)
    {
        if (!string.IsNullOrEmpty(folderPath) && !SelectedFolders.Contains(folderPath))
        {
            SelectedFolders.Add(folderPath);
        }
    }

    [RelayCommand]
    private void RemoveFolder(string folder)
    {
        SelectedFolders.Remove(folder);
    }

    [RelayCommand]
    private void ClearFolders()
    {
        SelectedFolders.Clear();
    }

    [RelayCommand]
    private async Task StartScan()
    {
        _logger.LogDebug("스캔 시작 요청: CanStartScan={CanStartScan}, FolderCount={FolderCount}", CanStartScan, SelectedFolders.Count);

        if (!CanStartScan)
        {
            return;
        }

        _logger.LogInformation("스캔 시작: {Folders}", string.Join(", ", SelectedFolders));

        var config = new DupSweep.Core.Models.ScanConfig
        {
            Directories = SelectedFolders.ToList(),
            UseHashComparison = UseHashComparison,
            UseSizeComparison = UseSizeComparison,
            UseResolutionComparison = UseResolutionComparison,
            UseImageSimilarity = UseImageSimilarity,
            UseVideoSimilarity = UseVideoSimilarity,
            MatchCreatedDate = MatchCreatedDate,
            MatchModifiedDate = MatchModifiedDate,
            ImageSimilarityThreshold = SimilarityThreshold,
            VideoSimilarityThreshold = VideoSimilarityThreshold,
            ScanAllFiles = ScanAllFiles,
            ScanImages = ScanImages,
            ScanVideos = ScanVideos,
            ScanAudio = ScanAudio,
            ScanDocuments = ScanDocuments,
            ParallelThreads = _settingsViewModel.ParallelThreads,
            ThumbnailSize = _settingsViewModel.ThumbnailSize,
            FfmpegPath = string.IsNullOrWhiteSpace(_settingsViewModel.FfmpegPath) ? null : _settingsViewModel.FfmpegPath
        };

        if (!string.IsNullOrWhiteSpace(config.FfmpegPath))
        {
            var ffprobePath = Path.Combine(Path.GetDirectoryName(config.FfmpegPath) ?? string.Empty, "ffprobe.exe");
            if (File.Exists(ffprobePath))
            {
                config.FfprobePath = ffprobePath;
            }
        }

        // FFmpeg 필요 여부 확인
        bool needsFfmpeg = UseVideoSimilarity && ScanVideos;
        bool hasFfmpeg = !string.IsNullOrWhiteSpace(config.FfmpegPath) && File.Exists(config.FfmpegPath);

        if (needsFfmpeg && !hasFfmpeg)
        {
            var result = MessageBox.Show(
                LanguageService.Instance.GetString("Home.FFmpegNotSet"),
                LanguageService.Instance.GetString("Home.FFmpegNotSetTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                _logger.LogDebug("FFmpeg 미설정으로 사용자가 스캔 취소");
                return;
            }
        }

        _scanViewModel.Reset();
        _messenger.Send(new NavigateMessage(NavigationTarget.Scan));

        var progress = new Progress<DupSweep.Core.Models.ScanProgress>(p =>
        {
            _scanViewModel.ApplyProgress(p);
        });

        try
        {
            _logger.LogDebug("scanService.StartScanAsync 호출");
            var scanResult = await _scanService.StartScanAsync(config, progress);
            _logger.LogInformation("스캔 완료: {Groups}개 그룹 발견", scanResult?.DuplicateGroups?.Count ?? 0);
            if (scanResult != null)
            {
                _resultsViewModel.LoadResults(scanResult);
                _messenger.Send(new NavigateMessage(NavigationTarget.Results));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "스캔 중 오류 발생");
            _scanViewModel.ApplyProgress(new DupSweep.Core.Models.ScanProgress
            {
                Phase = DupSweep.Core.Models.ScanPhase.Error,
                CurrentFile = string.Empty,
                TotalFiles = 0,
                ProcessedFiles = 0,
                DuplicateGroupsFound = 0,
                PotentialSavings = 0,
                ElapsedTime = TimeSpan.Zero,
                IsPaused = false,
                IsCancelled = false
            });
            _scanViewModel.StatusMessage = LanguageService.Instance.GetString("Common.Error", ex.Message);
        }
    }
}
