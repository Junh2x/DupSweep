using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<string> _selectedFolders = new();

    // 감지 조건
    [ObservableProperty]
    private bool _useHashComparison = true;

    [ObservableProperty]
    private bool _useSizeComparison = true;

    [ObservableProperty]
    private bool _useResolutionComparison;

    [ObservableProperty]
    private bool _useImageSimilarity = true;

    [ObservableProperty]
    private bool _useVideoSimilarity = true;

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
    private bool _scanImages = true;

    [ObservableProperty]
    private bool _scanVideos = true;

    [ObservableProperty]
    private bool _canStartScan;

    public HomeViewModel()
    {
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
        Console.WriteLine($"[StartScan] Called. CanStartScan={CanStartScan}, FolderCount={SelectedFolders.Count}");
        Debug.WriteLine($"[StartScan] Called. CanStartScan={CanStartScan}, FolderCount={SelectedFolders.Count}");

        if (!CanStartScan)
        {
            Console.WriteLine("[StartScan] CanStartScan is false, returning");
            return;
        }

        Console.WriteLine($"[StartScan] Starting scan with folders: {string.Join(", ", SelectedFolders)}");

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
            ScanImages = ScanImages,
            ScanVideos = ScanVideos
        };

        var settingsVm = App.Services.GetService(typeof(SettingsViewModel)) as SettingsViewModel;
        if (settingsVm != null)
        {
            config.ParallelThreads = settingsVm.ParallelThreads;
            config.ThumbnailSize = settingsVm.ThumbnailSize;
            config.FfmpegPath = string.IsNullOrWhiteSpace(settingsVm.FfmpegPath) ? null : settingsVm.FfmpegPath;
            if (!string.IsNullOrWhiteSpace(config.FfmpegPath))
            {
                var ffprobePath = Path.Combine(Path.GetDirectoryName(config.FfmpegPath) ?? string.Empty, "ffprobe.exe");
                if (File.Exists(ffprobePath))
                {
                    config.FfprobePath = ffprobePath;
                }
            }
        }

        // FFmpeg 필요 여부 확인
        bool needsFfmpeg = UseVideoSimilarity && ScanVideos;
        bool hasFfmpeg = !string.IsNullOrWhiteSpace(config.FfmpegPath) && File.Exists(config.FfmpegPath);

        if (needsFfmpeg && !hasFfmpeg)
        {
            var result = MessageBox.Show(
                "FFmpeg 경로가 설정되지 않았습니다.\n비디오 유사도 검사가 건너뛰어집니다.\n\n이대로 실행하시겠습니까?",
                "FFmpeg 미설정",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                Console.WriteLine("[StartScan] User cancelled due to missing FFmpeg");
                return;
            }
            Console.WriteLine("[StartScan] User confirmed to proceed without FFmpeg");
        }

        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        var scanVm = App.Services.GetService(typeof(ScanViewModel)) as ScanViewModel;
        var resultsVm = App.Services.GetService(typeof(ResultsViewModel)) as ResultsViewModel;
        var scanService = App.Services.GetService(typeof(DupSweep.Core.Services.Interfaces.IScanService)) as DupSweep.Core.Services.Interfaces.IScanService;

        if (scanVm == null || resultsVm == null || scanService == null || mainVm == null)
        {
            Console.WriteLine($"[StartScan] Service null check failed: scanVm={scanVm != null}, resultsVm={resultsVm != null}, scanService={scanService != null}, mainVm={mainVm != null}");
            return;
        }

        Console.WriteLine("[StartScan] All services resolved successfully");

        scanVm.Reset();
        mainVm.NavigateToScanCommand.Execute(null);

        var progress = new Progress<DupSweep.Core.Models.ScanProgress>(p =>
        {
            Console.WriteLine($"[StartScan] Progress: Phase={p.Phase}, Processed={p.ProcessedFiles}/{p.TotalFiles}");
            scanVm.ApplyProgress(p);
        });

        try
        {
            Console.WriteLine("[StartScan] Calling scanService.StartScanAsync...");
            var result = await scanService.StartScanAsync(config, progress);
            Console.WriteLine($"[StartScan] Scan completed. Groups found: {result?.DuplicateGroups?.Count ?? 0}");
            resultsVm.LoadResults(result);
            mainVm.NavigateToResultsCommand.Execute(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartScan] ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[StartScan] StackTrace: {ex.StackTrace}");
            scanVm.ApplyProgress(new DupSweep.Core.Models.ScanProgress
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
            scanVm.StatusMessage = $"Error: {ex.Message}";
        }
    }
}
