using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<string> _selectedFolders = new();

    [ObservableProperty]
    private bool _useHashComparison = true;

    [ObservableProperty]
    private bool _useImageSimilarity = true;

    [ObservableProperty]
    private bool _useVideoSimilarity = true;

    [ObservableProperty]
    private bool _useAudioSimilarity = true;

    [ObservableProperty]
    private double _similarityThreshold = 85;

    [ObservableProperty]
    private double _videoSimilarityThreshold = 85;

    [ObservableProperty]
    private double _audioSimilarityThreshold = 85;

    [ObservableProperty]
    private bool _matchCreatedDate;

    [ObservableProperty]
    private bool _matchModifiedDate;

    [ObservableProperty]
    private bool _scanImages = true;

    [ObservableProperty]
    private bool _scanVideos = true;

    [ObservableProperty]
    private bool _scanAudio = true;

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
        if (!CanStartScan)
        {
            return;
        }

        var config = new DupSweep.Core.Models.ScanConfig
        {
            Directories = SelectedFolders.ToList(),
            UseHashComparison = UseHashComparison,
            UseImageSimilarity = UseImageSimilarity,
            UseVideoSimilarity = UseVideoSimilarity,
            UseAudioSimilarity = UseAudioSimilarity,
            MatchCreatedDate = MatchCreatedDate,
            MatchModifiedDate = MatchModifiedDate,
            ImageSimilarityThreshold = SimilarityThreshold,
            VideoSimilarityThreshold = VideoSimilarityThreshold,
            AudioSimilarityThreshold = AudioSimilarityThreshold,
            ScanImages = ScanImages,
            ScanVideos = ScanVideos,
            ScanAudio = ScanAudio
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

        var mainVm = App.Services.GetService(typeof(MainViewModel)) as MainViewModel;
        var scanVm = App.Services.GetService(typeof(ScanViewModel)) as ScanViewModel;
        var resultsVm = App.Services.GetService(typeof(ResultsViewModel)) as ResultsViewModel;
        var scanService = App.Services.GetService(typeof(DupSweep.Core.Services.Interfaces.IScanService)) as DupSweep.Core.Services.Interfaces.IScanService;

        if (scanVm == null || resultsVm == null || scanService == null || mainVm == null)
        {
            return;
        }

        scanVm.Reset();
        mainVm.NavigateToScanCommand.Execute(null);

        var progress = new Progress<DupSweep.Core.Models.ScanProgress>(scanVm.ApplyProgress);
        try
        {
            var result = await scanService.StartScanAsync(config, progress);
            resultsVm.LoadResults(result);
            mainVm.NavigateToResultsCommand.Execute(null);
        }
        catch (Exception ex)
        {
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
