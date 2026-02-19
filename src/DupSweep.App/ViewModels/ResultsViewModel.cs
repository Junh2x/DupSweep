using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.Core.Services.Interfaces;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 스캔 결과 화면 ViewModel
/// 중복 파일 그룹 표시, 파일 선택, 삭제 기능 제공
///
/// 성능 설계:
/// - ICollectionView + GroupDescriptions 미사용 (가상화 파괴 방지)
/// - DisplayItems: 플랫 리스트로 ListView 가상화 보장
/// - _suppressSelectionUpdate: 일괄 선택 시 O(n²) 방지
/// </summary>
public partial class ResultsViewModel : ObservableObject
{
    private readonly IDeleteService _deleteService;
    private readonly SettingsViewModel _settingsViewModel;
    private bool _suppressSelectionUpdate;

    [ObservableProperty]
    private ObservableCollection<DuplicateGroupViewModel> _duplicateGroups = new();

    /// <summary>
    /// ListView에 바인딩되는 플랫 리스트.
    /// DuplicateGroups를 평탄화하고 IsFirstInGroup 플래그로 그룹 구분선 표시.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FileItemViewModel> _displayItems = new();

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
    }

    public void LoadResults(DupSweep.Core.Models.ScanResult result)
    {
        FocusedFile = null;

        if (!result.IsSuccessful)
        {
            DuplicateGroups = new();
            DisplayItems = new();
            HasResults = false;
            return;
        }

        _suppressSelectionUpdate = true;

        var tempGroups = new List<DuplicateGroupViewModel>();

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

            // List에 먼저 구성 후 일괄 설정 (CollectionChanged 이벤트 최소화)
            var files = new List<FileItemViewModel>();
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
                fileVm.PropertyChanged += FileVm_PropertyChanged;
                files.Add(fileVm);
            }

            groupVm.SetFiles(files);
            tempGroups.Add(groupVm);
            groupIndex++;
        }

        DuplicateGroups = new ObservableCollection<DuplicateGroupViewModel>(tempGroups);

        _suppressSelectionUpdate = false;

        RebuildDisplayItems();
        HasResults = DuplicateGroups.Count > 0;
        OnPropertyChanged(nameof(TotalFilesCount));
        UpdateSelectionStats();
    }

    private void FileVm_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FileItemViewModel.IsSelected) && !_suppressSelectionUpdate)
        {
            UpdateSelectionStats();
        }
    }

    /// <summary>
    /// DuplicateGroups를 필터 적용 후 플랫 리스트로 변환.
    /// IsFirstInGroup 플래그로 그룹 구분선 위치 표시.
    /// </summary>
    private void RebuildDisplayItems()
    {
        var items = new List<FileItemViewModel>();
        bool hasItems = false;

        foreach (var group in DuplicateGroups)
        {
            if (!PassesFilter(group)) continue;

            bool isFirst = true;
            foreach (var file in group.Files)
            {
                file.IsFirstInGroup = isFirst && hasItems;
                items.Add(file);
                isFirst = false;
                hasItems = true;
            }
        }

        DisplayItems = new ObservableCollection<FileItemViewModel>(items);
    }

    private bool PassesFilter(DuplicateGroupViewModel group)
    {
        return FilterType switch
        {
            "Images" => group.GroupType.Contains("Image", StringComparison.OrdinalIgnoreCase),
            "Videos" => group.GroupType.Contains("Video", StringComparison.OrdinalIgnoreCase),
            "Audio" => group.GroupType.Contains("Audio", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    [RelayCommand]
    private void CloseFocusedFile()
    {
        FocusedFile = null;
    }

    [RelayCommand]
    private void SelectFirst()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectFirst();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectNewest()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectNewest();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectOldest()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectOldest();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectLargest()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectLargest();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectSmallest()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectSmallest();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectHighestResolution()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectHighestResolution();
        }
        _suppressSelectionUpdate = false;
        UpdateSelectionStats();
    }

    [RelayCommand]
    private void SelectLowestResolution()
    {
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            group.SelectLowestResolution();
        }
        _suppressSelectionUpdate = false;
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
        _suppressSelectionUpdate = true;
        foreach (var group in DuplicateGroups)
        {
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
        }
        _suppressSelectionUpdate = false;
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

    partial void OnFilterTypeChanged(string value)
    {
        RebuildDisplayItems();
    }

    private void RemoveDeletedFiles(IEnumerable<FileItemViewModel> deletedFiles)
    {
        var deletedSet = new HashSet<FileItemViewModel>(deletedFiles);

        if (FocusedFile != null && deletedSet.Contains(FocusedFile))
        {
            FocusedFile = null;
        }

        foreach (var group in DuplicateGroups.ToList())
        {
            foreach (var file in deletedSet)
            {
                group.Files.Remove(file);
            }

            if (group.Files.Count < 2)
            {
                DuplicateGroups.Remove(group);
            }
        }

        RebuildDisplayItems();
        OnPropertyChanged(nameof(TotalFilesCount));
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

    public string GroupLabel => $"{FileCount} files · {GroupType} · {Similarity:0}%";

    /// <summary>
    /// 파일 목록 일괄 설정 (CollectionChanged 이벤트 최소화)
    /// </summary>
    public void SetFiles(IEnumerable<FileItemViewModel> files)
    {
        Files = new ObservableCollection<FileItemViewModel>(files);
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

    /// <summary>
    /// 그룹의 첫 번째 파일이면서 첫 번째 그룹이 아닌 경우 true.
    /// ListView에서 그룹 구분선 표시에 사용.
    /// </summary>
    [ObservableProperty]
    private bool _isFirstInGroup;

    public string FormattedSize => FormatFileSize(Size);
    public string Resolution => Width > 0 && Height > 0 ? $"{Width}\u00d7{Height}" : "-";

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
