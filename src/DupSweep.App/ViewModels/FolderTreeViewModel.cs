using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupSweep.App.Services;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 폴더 트리 분석 ViewModel
/// 폴더 용량을 계층적 바 차트로 시각화 (드릴다운 지원)
/// </summary>
public partial class FolderTreeViewModel : ObservableObject
{
    private const int MaxDepth = 5;
    private const double MinPercentThreshold = 1.0;
    private const int ParallelDepthThreshold = 2; // 이 깊이까지 병렬 처리

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

    // 현재 선택된 폴더의 파일/폴더 목록
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
            Title = LanguageService.Instance.GetString("FolderTree.SelectFolderToAnalyze")
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
        LoadingMessage = LanguageService.Instance.GetString("FolderTree.AnalyzingFolder");
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

            _rootNode = await Task.Run(() => CalculateFolderSizeParallel(folderPath, 0));

            if (_rootNode == null)
            {
                LoadingMessage = LanguageService.Instance.GetString("FolderTree.AnalysisFailed");
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
        var othersNodes = new List<FolderNode>();

        int colorIndex = 0;
        foreach (var child in sortedChildren)
        {
            double percent = _rootNode.Size > 0 ? (double)child.Size / _rootNode.Size * 100 : 0;

            if (percent < MinPercentThreshold)
            {
                othersSize += child.Size;
                othersCount++;
                othersNodes.Add(child);
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
                    Name = LanguageService.Instance.GetString("FolderTree.FileLabel"),
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
                Name = LanguageService.Instance.GetString("FolderTree.Others", othersCount),
                Size = othersSize,
                FormattedSize = FormatSize(othersSize),
                WidthPercent = _rootNode.Size > 0 ? (double)othersSize / _rootNode.Size * 100 : 0,
                OffsetPercent = currentOffset,
                Depth = 0,
                Color = "#9E9E9E",
                IsOthers = true,
                PercentOfTotal = TotalSize > 0 ? (double)othersSize / TotalSize * 100 : 0,
                FullPath = _rootNode.FullPath,
                OthersNodes = othersNodes,
                OthersIncludesFiles = _rootNode.FilesSize > 0 &&
                    (_rootNode.Size > 0 ? (double)_rootNode.FilesSize / _rootNode.Size * 100 : 0) < MinPercentThreshold
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

        // 기타 항목이면 기타 목록 표시
        if (item.IsOthers)
        {
            UpdateOthersFiles(item);
            UpdateBreadcrumbs();
            return;
        }

        // 하위 레벨 생성 (드릴다운)
        if (item.Node != null && item.Node.Children.Count > 0 && depth < MaxDepth - 1)
        {
            BuildNextLevel(item.Node, depth + 1);
        }

        // 브레드크럼 업데이트
        UpdateBreadcrumbs();

        // 파일/폴더 목록 업데이트
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
        var othersNodes = new List<FolderNode>();

        foreach (var child in sortedChildren)
        {
            double percent = parentNode.Size > 0 ? (double)child.Size / parentNode.Size * 100 : 0;

            if (percent < MinPercentThreshold)
            {
                othersSize += child.Size;
                othersCount++;
                othersNodes.Add(child);
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
        bool filesIncludedInOthers = false;
        if (parentNode.FilesSize > 0)
        {
            double filesPercent = parentNode.Size > 0 ? (double)parentNode.FilesSize / parentNode.Size * 100 : 0;
            if (filesPercent >= MinPercentThreshold)
            {
                level.Items.Add(new FolderBarItem
                {
                    Name = LanguageService.Instance.GetString("FolderTree.FileLabel"),
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
                filesIncludedInOthers = true;
            }
        }

        // 기타
        if (othersSize > 0)
        {
            level.Items.Add(new FolderBarItem
            {
                Name = LanguageService.Instance.GetString("FolderTree.Others", othersCount),
                Size = othersSize,
                FormattedSize = FormatSize(othersSize),
                WidthPercent = parentNode.Size > 0 ? (double)othersSize / parentNode.Size * 100 : 0,
                OffsetPercent = currentOffset,
                Depth = depth,
                Color = "#9E9E9E",
                IsOthers = true,
                PercentOfTotal = TotalSize > 0 ? (double)othersSize / TotalSize * 100 : 0,
                FullPath = parentNode.FullPath,
                OthersNodes = othersNodes,
                OthersIncludesFiles = filesIncludedInOthers
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
            var items = new List<FileInfoItem>();
            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            };

            // 폴더 목록 추가 (캐시된 크기 또는 실시간 계산)
            foreach (var dir in Directory.EnumerateDirectories(folderPath, "*", enumOptions))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);

                    // 해당 폴더의 크기 계산 (Node에서 가져오거나 실시간 계산)
                    long folderSize = 0;
                    if (item.Node != null)
                    {
                        // 대소문자 무시 비교
                        var childNode = item.Node.Children.FirstOrDefault(c =>
                            string.Equals(c.FullPath, dir, StringComparison.OrdinalIgnoreCase));
                        if (childNode != null)
                            folderSize = childNode.Size;
                    }

                    // Node에서 크기를 못 찾으면 빠르게 추정 (파일 크기만)
                    if (folderSize == 0)
                    {
                        try
                        {
                            folderSize = Directory.EnumerateFiles(dir, "*", new EnumerationOptions
                            {
                                RecurseSubdirectories = true,
                                IgnoreInaccessible = true,
                                AttributesToSkip = FileAttributes.System
                            }).Sum(f =>
                            {
                                try { return new FileInfo(f).Length; }
                                catch { return 0; }
                            });
                        }
                        catch { }
                    }

                    items.Add(new FileInfoItem
                    {
                        Name = dirInfo.Name,
                        Size = folderSize,
                        FormattedSize = FormatSize(folderSize),
                        Extension = "",
                        IsFolder = true
                    });
                }
                catch { }
            }

            // 파일 목록 추가
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", enumOptions))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    items.Add(new FileInfoItem
                    {
                        Name = fileInfo.Name,
                        Size = fileInfo.Length,
                        FormattedSize = FormatSize(fileInfo.Length),
                        Extension = fileInfo.Extension.ToUpperInvariant(),
                        IsFolder = false
                    });
                }
                catch { }
            }

            // 크기순 정렬 후 상위 50개만 표시
            var sortedItems = items.OrderByDescending(i => i.Size).Take(50).ToList();
            foreach (var fileItem in sortedItems)
            {
                CurrentFiles.Add(fileItem);
            }

            int totalFolders = items.Count(i => i.IsFolder);
            int totalFiles = items.Count(i => !i.IsFolder);
            int totalItems = totalFolders + totalFiles;
            CurrentFolderFileInfo = LanguageService.Instance.GetString("FolderTree.FolderFileSummary", totalFolders, totalFiles) +
                (totalItems > 50 ? " " + LanguageService.Instance.GetString("FolderTree.Top50Shown") : "");
        }
        catch (Exception ex)
        {
            CurrentFolderFileInfo = LanguageService.Instance.GetString("FolderTree.LoadListFailed", ex.Message);
        }
    }

    private void UpdateOthersFiles(FolderBarItem item)
    {
        CurrentFiles.Clear();

        if (item.OthersNodes == null && !item.OthersIncludesFiles)
        {
            CurrentFolderFileInfo = LanguageService.Instance.GetString("FolderTree.NoOtherItems");
            return;
        }

        try
        {
            var items = new List<FileInfoItem>();

            // 기타에 포함된 폴더들
            if (item.OthersNodes != null)
            {
                foreach (var node in item.OthersNodes)
                {
                    items.Add(new FileInfoItem
                    {
                        Name = node.Name,
                        Size = node.Size,
                        FormattedSize = FormatSize(node.Size),
                        Extension = "",
                        IsFolder = true
                    });
                }
            }

            // 기타에 포함된 파일들 (파일이 기타에 포함된 경우)
            if (item.OthersIncludesFiles && !string.IsNullOrEmpty(item.FullPath))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(item.FullPath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            items.Add(new FileInfoItem
                            {
                                Name = fileInfo.Name,
                                Size = fileInfo.Length,
                                FormattedSize = FormatSize(fileInfo.Length),
                                Extension = fileInfo.Extension.ToUpperInvariant(),
                                IsFolder = false
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 크기순 정렬 후 상위 50개만 표시
            var sortedItems = items.OrderByDescending(i => i.Size).Take(50).ToList();
            foreach (var fileItem in sortedItems)
            {
                CurrentFiles.Add(fileItem);
            }

            int folderCount = item.OthersNodes?.Count ?? 0;
            int fileCount = items.Count - folderCount;
            CurrentFolderFileInfo = LanguageService.Instance.GetString("FolderTree.OthersSummary", folderCount, fileCount) +
                (items.Count > 50 ? " " + LanguageService.Instance.GetString("FolderTree.Top50Shown") : "");
        }
        catch
        {
            CurrentFolderFileInfo = LanguageService.Instance.GetString("FolderTree.LoadOthersFailed");
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

    /// <summary>
    /// 병렬 처리를 사용하여 폴더 크기 계산 (속도 대폭 향상)
    /// </summary>
    private FolderNode? CalculateFolderSizeParallel(string path, int depth)
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

            // 폴더 카운터 증가 및 UI 업데이트
            var count = Interlocked.Increment(ref _folderCounter);
            if (count % 500 == 0)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    AnalyzedFolderCount = count;
                });
            }

            // 파일 크기 계산 (병렬)
            try
            {
                var fileEntries = Directory.EnumerateFiles(path).ToArray();
                if (fileEntries.Length > 0)
                {
                    long filesSize = 0;
                    int fileCount = 0;

                    if (fileEntries.Length > 100)
                    {
                        // 파일이 많으면 병렬 처리
                        var localSize = 0L;
                        var localCount = 0;
                        Parallel.ForEach(fileEntries,
                            () => (size: 0L, count: 0),
                            (file, state, local) =>
                            {
                                try
                                {
                                    var fi = new FileInfo(file);
                                    return (local.size + fi.Length, local.count + 1);
                                }
                                catch
                                {
                                    return local;
                                }
                            },
                            local =>
                            {
                                Interlocked.Add(ref localSize, local.size);
                                Interlocked.Add(ref localCount, local.count);
                            });
                        filesSize = localSize;
                        fileCount = localCount;
                    }
                    else
                    {
                        // 파일이 적으면 순차 처리
                        foreach (var file in fileEntries)
                        {
                            try
                            {
                                var fi = new FileInfo(file);
                                filesSize += fi.Length;
                                fileCount++;
                            }
                            catch { }
                        }
                    }

                    node.FilesSize = filesSize;
                    node.FileCount = fileCount;
                }
            }
            catch { }

            // 하위 폴더 처리
            if (depth < MaxDepth)
            {
                try
                {
                    var directories = Directory.EnumerateDirectories(path).ToArray();

                    if (depth < ParallelDepthThreshold && directories.Length > 1)
                    {
                        // 상위 레벨에서는 병렬로 하위 폴더 처리
                        var childNodes = new ConcurrentBag<FolderNode>();

                        Parallel.ForEach(directories, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                            dir =>
                            {
                                try
                                {
                                    var dirInfo = new DirectoryInfo(dir);
                                    if ((dirInfo.Attributes & FileAttributes.System) == FileAttributes.System)
                                        return;

                                    var childNode = CalculateFolderSizeParallel(dir, depth + 1);
                                    if (childNode != null)
                                    {
                                        childNodes.Add(childNode);
                                    }
                                }
                                catch { }
                            });

                        foreach (var childNode in childNodes)
                        {
                            node.Children.Add(childNode);
                            node.FolderCount++;
                        }
                    }
                    else
                    {
                        // 하위 레벨에서는 순차 처리
                        foreach (var dir in directories)
                        {
                            try
                            {
                                var dirInfo = new DirectoryInfo(dir);
                                if ((dirInfo.Attributes & FileAttributes.System) == FileAttributes.System)
                                    continue;

                                var childNode = CalculateFolderSizeParallel(dir, depth + 1);
                                if (childNode != null)
                                {
                                    node.Children.Add(childNode);
                                    node.FolderCount++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            else
            {
                // MaxDepth 초과 시 빠른 크기 계산
                node.FilesSize += CalculateTotalSizeParallel(path);
            }

            node.Size = node.FilesSize + node.Children.Sum(c => c.Size);
            return node;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 병렬 처리를 사용하여 총 크기 계산
    /// </summary>
    private long CalculateTotalSizeParallel(string path)
    {
        long total = 0;
        try
        {
            // 모든 파일을 재귀적으로 열거하고 병렬로 크기 합산
            var files = Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System
            });

            var localTotal = 0L;
            Parallel.ForEach(files,
                () => 0L,
                (file, state, local) =>
                {
                    try
                    {
                        return local + new FileInfo(file).Length;
                    }
                    catch
                    {
                        return local;
                    }
                },
                local => Interlocked.Add(ref localTotal, local));

            total = localTotal;
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

/// <summary>
/// 바 차트 레벨 (폴더 깊이별 바 집합)
/// </summary>
public partial class BarLevel : ObservableObject
{
    [ObservableProperty]
    private int _depth;

    [ObservableProperty]
    private ObservableCollection<FolderBarItem> _items = new();
}

/// <summary>
/// 폴더 바 항목 (개별 폴더의 시각화 데이터)
/// </summary>
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

    // 기타 항목에 포함된 노드들
    public List<FolderTreeViewModel.FolderNode>? OthersNodes { get; set; }

    // 기타 항목에 파일이 포함되어 있는지
    public bool OthersIncludesFiles { get; set; }

    public string FormattedPercent => $"{PercentOfTotal:0.0}%";

    // 툴팁용 정보
    public string TooltipText => $"{Name}\n{FormattedSize} ({PercentOfTotal:0.1}%)";
}

/// <summary>
/// 브레드크럼 항목 (현재 탐색 경로 표시)
/// </summary>
public partial class BreadcrumbItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private int _depth;
}

/// <summary>
/// 파일/폴더 정보 항목 (폴더 내 목록용)
/// </summary>
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

    [ObservableProperty]
    private bool _isFolder;
}
