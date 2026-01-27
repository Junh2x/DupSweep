using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 폴더 트리 뷰모델.
/// 폴더 구조와 용량 정보를 관리합니다.
/// </summary>
public partial class FolderTreeViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<FolderNodeViewModel> _rootNodes = new();

    [ObservableProperty]
    private FolderNodeViewModel? _selectedNode;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = string.Empty;

    /// <summary>
    /// 드라이브 목록을 로드합니다.
    /// </summary>
    public async Task LoadDrivesAsync()
    {
        IsLoading = true;
        LoadingMessage = "Loading drives...";

        try
        {
            RootNodes.Clear();

            await Task.Run(() =>
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .OrderBy(d => d.Name);

                foreach (var drive in drives)
                {
                    var node = new FolderNodeViewModel
                    {
                        Name = drive.Name,
                        FullPath = drive.RootDirectory.FullName,
                        IsExpanded = false,
                        IsDrive = true,
                        TotalSize = drive.TotalSize,
                        FreeSpace = drive.AvailableFreeSpace,
                        UsedSpace = drive.TotalSize - drive.AvailableFreeSpace,
                        DriveType = drive.DriveType.ToString(),
                        DriveFormat = drive.DriveFormat,
                        VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel
                    };

                    // 하위 폴더가 있는지 확인
                    try
                    {
                        node.HasChildren = Directory.EnumerateDirectories(node.FullPath).Any();
                    }
                    catch
                    {
                        node.HasChildren = false;
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        RootNodes.Add(node);
                    });
                }
            });
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    /// <summary>
    /// 특정 폴더의 하위 폴더를 로드합니다.
    /// </summary>
    public async Task LoadChildrenAsync(FolderNodeViewModel parentNode)
    {
        if (parentNode.IsLoaded || parentNode.IsLoading)
            return;

        parentNode.IsLoading = true;

        try
        {
            parentNode.Children.Clear();

            await Task.Run(() =>
            {
                try
                {
                    var directories = Directory.EnumerateDirectories(parentNode.FullPath)
                        .OrderBy(d => d);

                    foreach (var dir in directories)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dir);

                            // 시스템/숨김 폴더 제외
                            if ((dirInfo.Attributes & FileAttributes.System) == FileAttributes.System)
                                continue;

                            var node = new FolderNodeViewModel
                            {
                                Name = dirInfo.Name,
                                FullPath = dirInfo.FullName,
                                IsExpanded = false,
                                Parent = parentNode,
                                IsHidden = (dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden
                            };

                            // 하위 폴더가 있는지 확인
                            try
                            {
                                node.HasChildren = Directory.EnumerateDirectories(node.FullPath).Any();
                            }
                            catch
                            {
                                node.HasChildren = false;
                            }

                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                parentNode.Children.Add(node);
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // 접근 권한 없음 - 무시
                        }
                    }
                }
                catch (Exception)
                {
                    // 폴더 목록 조회 실패
                }
            });

            parentNode.IsLoaded = true;
        }
        finally
        {
            parentNode.IsLoading = false;
        }
    }

    /// <summary>
    /// 폴더 크기를 계산합니다.
    /// </summary>
    public async Task CalculateFolderSizeAsync(FolderNodeViewModel node)
    {
        if (node.IsSizeCalculating)
            return;

        node.IsSizeCalculating = true;
        node.SizeCalculationProgress = 0;

        try
        {
            long totalSize = 0;
            int fileCount = 0;
            int folderCount = 0;

            await Task.Run(() =>
            {
                CalculateSizeRecursive(node.FullPath, ref totalSize, ref fileCount, ref folderCount);
            });

            node.CalculatedSize = totalSize;
            node.FileCount = fileCount;
            node.FolderCount = folderCount;
            node.IsSizeCalculated = true;
        }
        finally
        {
            node.IsSizeCalculating = false;
            node.SizeCalculationProgress = 100;
        }
    }

    private void CalculateSizeRecursive(string path, ref long totalSize, ref int fileCount, ref int folderCount)
    {
        try
        {
            // 파일 크기 합산
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                    fileCount++;
                }
                catch
                {
                    // 파일 접근 실패 - 무시
                }
            }

            // 하위 폴더 재귀 처리
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if ((dirInfo.Attributes & FileAttributes.System) != FileAttributes.System)
                    {
                        folderCount++;
                        CalculateSizeRecursive(dir, ref totalSize, ref fileCount, ref folderCount);
                    }
                }
                catch
                {
                    // 폴더 접근 실패 - 무시
                }
            }
        }
        catch
        {
            // 경로 접근 실패
        }
    }
}

/// <summary>
/// 폴더 노드 뷰모델
/// </summary>
public partial class FolderNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasChildren;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _isSizeCalculating;

    [ObservableProperty]
    private bool _isSizeCalculated;

    [ObservableProperty]
    private int _sizeCalculationProgress;

    [ObservableProperty]
    private ObservableCollection<FolderNodeViewModel> _children = new();

    [ObservableProperty]
    private FolderNodeViewModel? _parent;

    // 드라이브 전용 속성
    [ObservableProperty]
    private bool _isDrive;

    [ObservableProperty]
    private long _totalSize;

    [ObservableProperty]
    private long _freeSpace;

    [ObservableProperty]
    private long _usedSpace;

    [ObservableProperty]
    private string _driveType = string.Empty;

    [ObservableProperty]
    private string _driveFormat = string.Empty;

    [ObservableProperty]
    private string _volumeLabel = string.Empty;

    // 폴더 크기 정보
    [ObservableProperty]
    private long _calculatedSize;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _folderCount;

    /// <summary>
    /// 포맷된 크기 문자열
    /// </summary>
    public string FormattedSize => FormatSize(IsDrive ? UsedSpace : CalculatedSize);

    /// <summary>
    /// 총 용량 포맷
    /// </summary>
    public string FormattedTotalSize => FormatSize(TotalSize);

    /// <summary>
    /// 여유 공간 포맷
    /// </summary>
    public string FormattedFreeSpace => FormatSize(FreeSpace);

    /// <summary>
    /// 사용률 (0-100)
    /// </summary>
    public double UsagePercent => TotalSize > 0 ? (double)UsedSpace / TotalSize * 100 : 0;

    /// <summary>
    /// 아이콘 종류
    /// </summary>
    public string IconKind
    {
        get
        {
            if (IsDrive)
            {
                return DriveType switch
                {
                    "Fixed" => "Harddisk",
                    "Removable" => "UsbFlashDrive",
                    "Network" => "ServerNetwork",
                    "CDRom" => "DiscPlayer",
                    _ => "Harddisk"
                };
            }
            return IsExpanded ? "FolderOpen" : "Folder";
        }
    }

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string DisplayName => IsDrive
        ? $"{VolumeLabel} ({Name.TrimEnd('\\')})"
        : Name;

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

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(IconKind));
    }

    partial void OnUsedSpaceChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedSize));
        OnPropertyChanged(nameof(UsagePercent));
    }

    partial void OnCalculatedSizeChanged(long value)
    {
        OnPropertyChanged(nameof(FormattedSize));
    }
}
