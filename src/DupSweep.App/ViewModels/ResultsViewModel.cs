using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 스캔 결과 화면 ViewModel
/// 중복 파일 그룹 표시, 파일 선택, 삭제 기능 제공
/// </summary>
public partial class ResultsViewModel : ObservableObject
{
    private readonly IDeleteService _deleteService;
    private readonly SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _allFiles = new();

    public ICollectionView DuplicateGroupsView { get; }
    public ICollectionView AllFilesView { get; }

    [ObservableProperty]
    private string _filterType = "All";

    [ObservableProperty]
    private int _selectedFilesCount;

    [ObservableProperty]
    private long _potentialSavings;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private FileItemViewModel? _focusedFile;

    // Auto Select Radio Button States
    [ObservableProperty]
    private bool _autoSelectLargest = true;

    [ObservableProperty]
    private bool _autoSelectSmallest;

    [ObservableProperty]
    private bool _autoSelectNewest;

    [ObservableProperty]
    private bool _autoSelectOldest;

    [ObservableProperty]
    private bool _autoSelectHighRes;

    [ObservableProperty]
    private bool _autoSelectLowRes;

    public string FormattedPotentialSavings => FormatFileSize(PotentialSavings);

    public int TotalFilesCount => DuplicateGroups.Sum(g => g.FileCount);

    public ResultsViewModel(IDeleteService deleteService, SettingsViewModel settingsViewModel)
    {
        _deleteService = deleteService;
        _settingsViewModel = settingsViewModel;

        DuplicateGroupsView = CollectionViewSource.GetDefaultView(DuplicateGroups);
        DuplicateGroupsView.Filter = FilterGroup;
        DuplicateGroups.CollectionChanged += (_, _) =>
        {
            HasResults = DuplicateGroups.Count > 0;
            OnPropertyChanged(nameof(TotalFilesCount));
            DuplicateGroupsView.Refresh();
        };

        AllFilesView = CollectionViewSource.GetDefaultView(AllFiles);
        AllFilesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(FileItemViewModel.GroupId)));
    }

    public void LoadResults(DupSweep.Core.Models.ScanResult result)
    {
        DuplicateGroups.Clear();
        AllFiles.Clear();
        FocusedFile = null;

        if (!result.IsSuccessful)
        {
            HasResults = false;
            return;
        }

        int groupIndex = 0;
        foreach (var group in result.DuplicateGroups)
        {
            var groupVm = new DuplicateGroupViewModel
            {
                GroupType = group.Type switch
                {
                    DupSweep.Core.Models.DuplicateType.SimilarImage => "Similar Image",
                    DupSweep.Core.Models.DuplicateType.SimilarVideo => "Similar Video",
                    DupSweep.Core.Models.DuplicateType.SimilarAudio => "Similar Audio",
                    _ => "Exact Match"
                },
                Similarity = group.Similarity
            };

            foreach (var file in group.Files)
            {
                var fileVm = new FileItemViewModel
                {
                    FileName = file.FileName,
                    FilePath = file.FilePath,
                    Size = file.Size,
                    ModifiedDate = file.ModifiedDate,
                    CreatedDate = file.CreatedDate,
                    ThumbnailPath = file.ThumbnailPath,
                    ThumbnailData = file.ThumbnailData,
                    Width = file.Width,
                    Height = file.Height,
                    Hash = TruncateHash(file.FullHash ?? file.QuickHash),
                    GroupId = groupIndex,
                    ParentGroup = groupVm
                };
                fileVm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(FileItemViewModel.IsSelected))
                    {
                        UpdateSelectionStats();
                    }
                };
                groupVm.Files.Add(fileVm);
                AllFiles.Add(fileVm);
            }

            DuplicateGroups.Add(groupVm);
            groupIndex++;
        }

        HasResults = DuplicateGroups.Count > 0;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void CloseFocusedFile()
    {
        FocusedFile = null;
    }

    [RelayCommand]
    private void SelectFirst()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectFirst();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectNewest()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectNewest();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectOldest()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectOldest();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectLargest()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectLargest();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectSmallest()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectSmallest();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectHighestResolution()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectHighestResolution();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectLowestResolution()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SelectLowestResolution();
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void ApplyAutoSelect()
    {
        if (AutoSelectLargest)
            SelectLargest();
        else if (AutoSelectSmallest)
            SelectSmallest();
        else if (AutoSelectNewest)
            SelectNewest();
        else if (AutoSelectOldest)
            SelectOldest();
        else if (AutoSelectHighRes)
            SelectHighestResolution();
        else if (AutoSelectLowRes)
            SelectLowestResolution();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var group in DuplicateGroups)
        {
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
        }
        UpdateSelectionStats();
    }

    [RelayCommand]
    private async Task MoveToTrash()
    {
        var selectedFiles = DuplicateGroups
            .SelectMany(g => g.Files)
            .Where(f => f.IsSelected)
            .ToList();

        if (selectedFiles.Count == 0)
        {
            return;
        }

        if (!ConfirmDeletion($"Move {selectedFiles.Count} files to trash?"))
        {
            return;
        }

        await _deleteService.MoveToTrashAsync(selectedFiles.Select(f => f.FilePath), CancellationToken.None);
        RemoveDeletedFiles(selectedFiles);
    }

    [RelayCommand]
    private async Task DeletePermanently()
    {
        var selectedFiles = DuplicateGroups
            .SelectMany(g => g.Files)
            .Where(f => f.IsSelected)
            .ToList();

        if (selectedFiles.Count == 0)
        {
            return;
        }

        if (!ConfirmDeletion($"Permanently delete {selectedFiles.Count} files?"))
        {
            return;
        }

        await _deleteService.DeletePermanentlyAsync(selectedFiles.Select(f => f.FilePath), CancellationToken.None);
        RemoveDeletedFiles(selectedFiles);
    }

    private void UpdateSelectionStats()
    {
        var selectedFiles = DuplicateGroups
            .SelectMany(g => g.Files)
            .Where(f => f.IsSelected);

        SelectedFilesCount = selectedFiles.Count();
        PotentialSavings = selectedFiles.Sum(f => f.Size);
        OnPropertyChanged(nameof(FormattedPotentialSavings));
        HasResults = DuplicateGroups.Count > 0;
    }

    private bool FilterGroup(object obj)
    {
        if (obj is not DuplicateGroupViewModel group)
        {
            return false;
        }

        return FilterType switch
        {
            "Images" => group.GroupType.Contains("Image", StringComparison.OrdinalIgnoreCase),
            "Videos" => group.GroupType.Contains("Video", StringComparison.OrdinalIgnoreCase),
            "Audio" => group.GroupType.Contains("Audio", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    partial void OnFilterTypeChanged(string value)
    {
        DuplicateGroupsView.Refresh();
    }

    private void RemoveDeletedFiles(IEnumerable<FileItemViewModel> deletedFiles)
    {
        var deletedList = deletedFiles.ToList();

        // AllFiles에서도 제거
        foreach (var file in deletedList)
        {
            AllFiles.Remove(file);
        }

        // FocusedFile이 삭제된 경우 해제
        if (FocusedFile != null && deletedList.Contains(FocusedFile))
        {
            FocusedFile = null;
        }

        foreach (var group in DuplicateGroups.ToList())
        {
            foreach (var file in deletedList)
            {
                if (group.Files.Contains(file))
                {
                    group.Files.Remove(file);
                }
            }

            if (group.Files.Count < 2)
            {
                DuplicateGroups.Remove(group);
            }
        }

        UpdateSelectionStats();
    }

    private bool ConfirmDeletion(string message)
    {
        if (!_settingsViewModel.ShowConfirmationDialog)
        {
            return true;
        }

        var result = System.Windows.MessageBox.Show(
            message,
            "Confirm",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return result == System.Windows.MessageBoxResult.Yes;
    }

    private static string TruncateHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return "-";
        }
        return hash.Length > 16 ? hash[..16] + "..." : hash;
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

/// <summary>
/// 중복 파일 그룹 ViewModel
/// 동일/유사 파일 그룹의 표시 및 선택 로직 담당
/// </summary>
public partial class DuplicateGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _files = new();

    [ObservableProperty]
    private string _groupType = "Exact Match";

    [ObservableProperty]
    private double _similarity = 100;

    public int FileCount => Files.Count;
    public long TotalSize => Files.Sum(f => f.Size);

    /// <summary>
    /// 그룹 구분선에 표시할 라벨 (예: "3 files · Similar Image · 96%")
    /// </summary>
    public string GroupLabel => $"{FileCount} files · {GroupType} · {Similarity:0}%";

    public DuplicateGroupViewModel()
    {
        Files.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(FileCount));
            OnPropertyChanged(nameof(TotalSize));
            OnPropertyChanged(nameof(GroupLabel));
        };
    }

    public void SelectFirst()
    {
        if (Files.Count == 0)
        {
            return;
        }

        for (int i = 0; i < Files.Count; i++)
        {
            Files[i].IsSelected = i > 0;
        }
    }

    public void SelectNewest()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var newest = Files.OrderByDescending(f => f.ModifiedDate).First();
        foreach (var file in Files)
        {
            file.IsSelected = file != newest;
        }
    }

    public void SelectOldest()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var oldest = Files.OrderBy(f => f.ModifiedDate).First();
        foreach (var file in Files)
        {
            file.IsSelected = file != oldest;
        }
    }

    public void SelectLargest()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var largest = Files.OrderByDescending(f => f.Size).First();
        foreach (var file in Files)
        {
            file.IsSelected = file != largest;
        }
    }

    public void SelectSmallest()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var smallest = Files.OrderBy(f => f.Size).First();
        foreach (var file in Files)
        {
            file.IsSelected = file != smallest;
        }
    }

    public void SelectHighestResolution()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var highest = Files.OrderByDescending(f => f.Width * f.Height).First();
        foreach (var file in Files)
        {
            file.IsSelected = file != highest;
        }
    }

    public void SelectLowestResolution()
    {
        if (Files.Count == 0)
        {
            return;
        }

        var lowest = Files.Where(f => f.Width > 0 && f.Height > 0)
                          .OrderBy(f => f.Width * f.Height)
                          .FirstOrDefault();

        // 해상도 정보가 없는 경우 첫 번째 파일 선택
        if (lowest == null)
        {
            SelectFirst();
            return;
        }

        foreach (var file in Files)
        {
            file.IsSelected = file != lowest;
        }
    }
}

/// <summary>
/// 파일 항목 ViewModel
/// 개별 파일의 정보 및 선택 상태 관리
/// </summary>
public partial class FileItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private DateTime _modifiedDate;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private byte[]? _thumbnailData;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private DateTime _createdDate;

    [ObservableProperty]
    private string _hash = string.Empty;

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private DuplicateGroupViewModel? _parentGroup;

    public string FormattedSize => FormatFileSize(Size);
    public string Resolution => Width > 0 && Height > 0 ? $"{Width}×{Height}" : "-";

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
}
