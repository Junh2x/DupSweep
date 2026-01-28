using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

public partial class FolderTreeViewModel : ObservableObject
{
    private const int MaxDepth = 5;
    private const double MinPercentThreshold = 1.0;

    [ObservableProperty]
    private string _selectedFolderPath = string.Empty;

    [ObservableProperty]
    private string _selectedFolderName = string.Empty;

    [ObservableProperty]
    private long _totalSize;

    [ObservableProperty]
    private string _formattedTotalSize = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BarLevel> _barLevels = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private int _analyzedFolderCount;

    // 브레드크럼 경로
    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    // 현재 선택된 폴더의 파일 목록
    [ObservableProperty]
    private ObservableCollection<FileInfoItem> _currentFiles = new();

    [ObservableProperty]
    private string _currentFolderFileInfo = string.Empty;

    private FolderNode? _rootNode;
    private int _folderCounter;
    private readonly Dictionary<int, FolderBarItem> _selectedItems = new();

    [RelayCommand]
    public async Task SelectFolderAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "분석할 폴더 선택"
        };

        if (dialog.ShowDialog() == true)
        {
            await AnalyzeFolderAsync(dialog.FolderName);
        }
    }

    public async Task AnalyzeFolderAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return;

        IsLoading = true;
        LoadingMessage = "폴더 분석 중...";
        HasData = false;
        BarLevels.Clear();
        Breadcrumbs.Clear();
        CurrentFiles.Clear();
        _selectedItems.Clear();
        _folderCounter = 0;
        AnalyzedFolderCount = 0;

        try
        {
            SelectedFolderPath = folderPath;
            SelectedFolderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(SelectedFolderName))
                SelectedFolderName = folderPath;

            _rootNode = await Task.Run(() => CalculateFolderSize(folderPath, 0, true));

            if (_rootNode == null)
            {
                LoadingMessage = "폴더 분석 실패";
                return;
            }

            TotalSize = _rootNode.Size;
            FormattedTotalSize = FormatSize(TotalSize);

            // 루트 레벨 (L0) 생성
            BuildLevel0();

            HasData = BarLevels.Count > 0;

            // 브레드크럼 초기화
            UpdateBreadcrumbs();
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    private void BuildLevel0()
    {
        if (_rootNode == null) return;

        BarLevels.Clear();
        var level = new BarLevel { Depth = 0 };

        var sortedChildren = _rootNode.Children.OrderByDescending(c => c.Size).ToList();
        double currentOffset = 0;
        long othersSize = 0;
        int othersCount = 0;

        int colorIndex = 0;
        foreach (var child in sortedChildren)
        {
            double percent = _rootNode.Size > 0 ? (double)child.Size / _rootNode.Size * 100 : 0;

            if (percent < MinPercentThreshold)
            {
                othersSize += child.Size;
                othersCount++;
            }
            else
            {
                var item = CreateBarItem(child, 0, currentOffset, percent, colorIndex++);
                level.Items.Add(item);
                currentOffset += percent;
            }
        }

        // 파일
        if (_rootNode.FilesSize > 0)
        {
            double filesPercent = _rootNode.Size > 0 ? (double)_rootNode.FilesSize / _rootNode.Size * 100 : 0;
            if (filesPercent >= MinPercentThreshold)
            {
                level.Items.Add(new FolderBarItem
                {
                    Name = "(파일)",
                    FullPath = _rootNode.FullPath,
                    Size = _rootNode.FilesSize,
                    FormattedSize = FormatSize(_rootNode.FilesSize),
                    WidthPercent = filesPercent,
                    OffsetPercent = currentOffset,
                    Depth = 0,
                    Color = "#78909C",
                    IsFiles = true,
                    FileCount = _rootNode.FileCount,
                    PercentOfTotal = filesPercent
                });
                currentOffset += filesPercent;
            }
            else
            {
                othersSize += _rootNode.FilesSize;
                othersCount++;
            }
        }

        // 기타
        if (othersSize > 0)
        {
            level.Items.Add(new FolderBarItem
            {
                Name = $"기타 ({othersCount})",
                Size = othersSize,
                FormattedSize = FormatSize(othersSize),
                WidthPercent = _rootNode.Size > 0 ? (double)othersSize / _rootNode.Size * 100 : 0,
                OffsetPercent = currentOffset,
                Depth = 0,
                Color = "#9E9E9E",
                IsOthers = true,
                PercentOfTotal = TotalSize > 0 ? (double)othersSize / TotalSize * 100 : 0
            });
        }

        BarLevels.Add(level);
    }

    private FolderBarItem CreateBarItem(FolderNode node, int depth, double offset, double widthPercent, int colorIndex)
    {
        return new FolderBarItem
        {
            Name = node.Name,
            FullPath = node.FullPath,
            Size = node.Size,
            FormattedSize = FormatSize(node.Size),
            WidthPercent = widthPercent,
            OffsetPercent = offset,
            Depth = depth,
            Color = GetColorForDepth(depth, colorIndex),
            FileCount = node.FileCount,
            FolderCount = node.FolderCount,
            PercentOfTotal = TotalSize > 0 ? (double)node.Size / TotalSize * 100 : 0,
            Node = node
        };
    }

    public void SelectItem(FolderBarItem item)
    {
        if (item.IsOthers) return;

        int depth = item.Depth;

        // 같은 레벨의 이전 선택 해제
        if (BarLevels.Count > depth)
        {
            foreach (var i in BarLevels[depth].Items)
            {
                i.IsSelected = false;
            }
        }

        // 현재 아이템 선택
        item.IsSelected = true;
        _selectedItems[depth] = item;

        // 하위 레벨 모두 제거
        while (BarLevels.Count > depth + 1)
        {
            BarLevels.RemoveAt(BarLevels.Count - 1);
        }

        // 하위 레벨의 선택 기록 제거
        var keysToRemove = _selectedItems.Keys.Where(k => k > depth).ToList();
        foreach (var key in keysToRemove)
        {
            _selectedItems.Remove(key);
        }

        // 하위 레벨 생성 (드릴다운)
        if (item.Node != null && item.Node.Children.Count > 0 && depth < MaxDepth - 1)
        {
            BuildNextLevel(item.Node, depth + 1);
        }

        // 브레드크럼 업데이트
        UpdateBreadcrumbs();

        // 파일 목록 업데이트
        UpdateCurrentFiles(item);
    }

    private void BuildNextLevel(FolderNode parentNode, int depth)
    {
        var level = new BarLevel { Depth = depth };
        var sortedChildren = parentNode.Children.OrderByDescending(c => c.Size).ToList();

        double currentOffset = 0;
        long othersSize = 0;
        int othersCount = 0;
        int colorIndex = 0;

        foreach (var child in sortedChildren)
        {
            double percent = parentNode.Size > 0 ? (double)child.Size / parentNode.Size * 100 : 0;

            if (percent < MinPercentThreshold)
            {
                othersSize += child.Size;
                othersCount++;
            }
            else
            {
                var item = new FolderBarItem
                {
                    Name = child.Name,
                    FullPath = child.FullPath,
                    Size = child.Size,
                    FormattedSize = FormatSize(child.Size),
                    WidthPercent = percent,
                    OffsetPercent = currentOffset,
                    Depth = depth,
                    Color = GetColorForDepth(depth, colorIndex++),
                    FileCount = child.FileCount,
                    FolderCount = child.FolderCount,
                    PercentOfTotal = TotalSize > 0 ? (double)child.Size / TotalSize * 100 : 0,
                    Node = child
                };
                level.Items.Add(item);
                currentOffset += percent;
            }
        }

        // 파일
        if (parentNode.FilesSize > 0)
        {
            double filesPercent = parentNode.Size > 0 ? (double)parentNode.FilesSize / parentNode.Size * 100 : 0;
            if (filesPercent >= MinPercentThreshold)
            {
                level.Items.Add(new FolderBarItem
                {
                    Name = "(파일)",
                    FullPath = parentNode.FullPath,
                    Size = parentNode.FilesSize,
                    FormattedSize = FormatSize(parentNode.FilesSize),
                    WidthPercent = filesPercent,
                    OffsetPercent = currentOffset,
                    Depth = depth,
                    Color = "#78909C",
                    IsFiles = true,
                    FileCount = parentNode.FileCount,
                    PercentOfTotal = TotalSize > 0 ? (double)parentNode.FilesSize / TotalSize * 100 : 0
                });
                currentOffset += filesPercent;
            }
            else
            {
                othersSize += parentNode.FilesSize;
                othersCount++;
            }
        }

        // 기타
        if (othersSize > 0)
        {
            level.Items.Add(new FolderBarItem
            {
                Name = $"기타 ({othersCount})",
                Size = othersSize,
                FormattedSize = FormatSize(othersSize),
                WidthPercent = parentNode.Size > 0 ? (double)othersSize / parentNode.Size * 100 : 0,
                OffsetPercent = currentOffset,
                Depth = depth,
                Color = "#9E9E9E",
                IsOthers = true,
                PercentOfTotal = TotalSize > 0 ? (double)othersSize / TotalSize * 100 : 0
            });
        }

        BarLevels.Add(level);
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();

        // 루트
        Breadcrumbs.Add(new BreadcrumbItem
        {
            Name = SelectedFolderName,
            FullPath = SelectedFolderPath,
            Depth = -1
        });

        // 선택된 경로
        for (int i = 0; i < MaxDepth; i++)
        {
            if (_selectedItems.TryGetValue(i, out var item) && !item.IsFiles && !item.IsOthers)
            {
                Breadcrumbs.Add(new BreadcrumbItem
                {
                    Name = item.Name,
                    FullPath = item.FullPath ?? string.Empty,
                    Depth = i
                });
            }
            else
            {
                break;
            }
        }
    }

    private void UpdateCurrentFiles(FolderBarItem item)
    {
        CurrentFiles.Clear();

        string? folderPath = item.FullPath;
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            CurrentFolderFileInfo = string.Empty;
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(folderPath)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.Length)
                .Take(20)
                .ToList();

            foreach (var file in files)
            {
                CurrentFiles.Add(new FileInfoItem
                {
                    Name = file.Name,
                    Size = file.Length,
                    FormattedSize = FormatSize(file.Length),
                    Extension = file.Extension.ToUpperInvariant()
                });
            }

            int totalFiles = Directory.EnumerateFiles(folderPath).Count();
            CurrentFolderFileInfo = $"{totalFiles}개 파일" + (totalFiles > 20 ? " (상위 20개 표시)" : "");
        }
        catch
        {
            CurrentFolderFileInfo = "파일 목록을 불러올 수 없습니다";
        }
    }

    public void NavigateToBreadcrumb(BreadcrumbItem crumb)
    {
        if (crumb.Depth < 0)
        {
            // 루트로 이동 - 모든 선택 해제
            foreach (var level in BarLevels)
            {
                foreach (var item in level.Items)
                {
                    item.IsSelected = false;
                }
            }
            while (BarLevels.Count > 1)
            {
                BarLevels.RemoveAt(BarLevels.Count - 1);
            }
            _selectedItems.Clear();
            UpdateBreadcrumbs();
            CurrentFiles.Clear();
            CurrentFolderFileInfo = string.Empty;
        }
        else if (_selectedItems.TryGetValue(crumb.Depth, out var item))
        {
            // 해당 레벨까지만 유지
            while (BarLevels.Count > crumb.Depth + 2)
            {
                BarLevels.RemoveAt(BarLevels.Count - 1);
            }
            var keysToRemove = _selectedItems.Keys.Where(k => k > crumb.Depth).ToList();
            foreach (var key in keysToRemove)
            {
                _selectedItems.Remove(key);
            }
            UpdateBreadcrumbs();
            UpdateCurrentFiles(item);
        }
    }

    private FolderNode? CalculateFolderSize(string path, int depth, bool updateUI = false)
    {
        try
        {
            var node = new FolderNode
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                Depth = depth
            };

            if (string.IsNullOrEmpty(node.Name))
                node.Name = path;

            if (updateUI)
            {
                _folderCounter++;
                if (_folderCounter % 100 == 0)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        AnalyzedFolderCount = _folderCounter;
                    });
                }
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        node.FilesSize += fileInfo.Length;
                        node.FileCount++;
                    }
                    catch { }
                }
            }
            catch { }

            if (depth < MaxDepth)
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(path))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            if ((dirInfo.Attributes & FileAttributes.System) == FileAttributes.System)
                                continue;

                            var childNode = CalculateFolderSize(dir, depth + 1, updateUI);
                            if (childNode != null)
                            {
                                node.Children.Add(childNode);
                                node.FolderCount++;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            else
            {
                node.FilesSize += CalculateTotalSizeRecursive(path);
            }

            node.Size = node.FilesSize + node.Children.Sum(c => c.Size);
            return node;
        }
        catch
        {
            return null;
        }
    }

    private long CalculateTotalSizeRecursive(string path)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if ((dirInfo.Attributes & FileAttributes.System) != FileAttributes.System)
                        total += CalculateTotalSizeRecursive(dir);
                }
                catch { }
            }
        }
        catch { }
        return total;
    }

    private static readonly string[] DepthColors = new[]
    {
        "#2196F3", "#42A5F5", "#64B5F6", "#90CAF9",
        "#4CAF50", "#66BB6A", "#81C784", "#A5D6A7",
        "#FF9800", "#FFA726", "#FFB74D", "#FFCC80",
        "#E91E63", "#EC407A", "#F06292", "#F48FB1",
        "#9C27B0", "#AB47BC", "#BA68C8", "#CE93D8"
    };

    private string GetColorForDepth(int depth, int index)
    {
        int baseIndex = (depth * 4) % DepthColors.Length;
        return DepthColors[(baseIndex + index) % DepthColors.Length];
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
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

    public class FolderNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public long FilesSize { get; set; }
        public int FileCount { get; set; }
        public int FolderCount { get; set; }
        public int Depth { get; set; }
        public List<FolderNode> Children { get; } = new();
    }
}

public partial class BarLevel : ObservableObject
{
    [ObservableProperty]
    private int _depth;

    [ObservableProperty]
    private ObservableCollection<FolderBarItem> _items = new();
}

public partial class FolderBarItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _fullPath;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private double _widthPercent;

    [ObservableProperty]
    private double _offsetPercent;

    [ObservableProperty]
    private double _percentOfTotal;

    [ObservableProperty]
    private int _depth;

    [ObservableProperty]
    private string _color = "#2196F3";

    [ObservableProperty]
    private bool _isFiles;

    [ObservableProperty]
    private bool _isOthers;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _folderCount;

    public FolderTreeViewModel.FolderNode? Node { get; set; }

    public string FormattedPercent => $"{PercentOfTotal:0.0}%";

    // 툴팁용 정보
    public string TooltipText => $"{Name}\n{FormattedSize} ({PercentOfTotal:0.1}%)";
}

public partial class BreadcrumbItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private int _depth;
}

public partial class FileInfoItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private string _formattedSize = string.Empty;

    [ObservableProperty]
    private string _extension = string.Empty;
}
