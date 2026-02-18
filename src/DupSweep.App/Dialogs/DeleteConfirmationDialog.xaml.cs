using System.IO;
using System.Windows;
using System.Windows.Media;
using DupSweep.App.Services;
using DupSweep.Core.Models;

namespace DupSweep.App.Dialogs;

/// <summary>
/// 삭제 확인 다이얼로그.
/// 삭제할 파일 목록, 드라이런 옵션을 제공합니다.
/// </summary>
public partial class DeleteConfirmationDialog : Window
{
    private readonly List<DeleteFileItem> _files;
    private readonly bool _isPermanent;

    public bool IsConfirmed { get; private set; }
    public bool IsDryRun => DryRunCheckBox.IsChecked == true;

    public DeleteConfirmationDialog(
        IEnumerable<string> filePaths,
        bool isPermanent,
        DeleteValidationResult? validationResult = null)
    {
        InitializeComponent();

        _files = new List<DeleteFileItem>();
        _isPermanent = isPermanent;

        LoadFiles(filePaths, validationResult);
        UpdateUI();

        // 드래그 가능하게 설정
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        };
    }

    private void LoadFiles(IEnumerable<string> filePaths, DeleteValidationResult? validationResult)
    {
        var blockedPaths = validationResult?.BlockedFiles
            .Select(b => b.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        var warnings = validationResult?.Warnings
            .GroupBy(w => w.FilePath)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, List<FileWarning>>();

        foreach (var filePath in filePaths)
        {
            var fileInfo = new FileInfo(filePath);
            var isBlocked = blockedPaths.Contains(filePath);
            var fileWarnings = warnings.TryGetValue(filePath, out var w) ? w : new List<FileWarning>();

            var item = new DeleteFileItem
            {
                FileName = Path.GetFileName(filePath),
                DirectoryPath = Path.GetDirectoryName(filePath) ?? string.Empty,
                FullPath = filePath,
                Size = fileInfo.Exists ? fileInfo.Length : 0,
                IsBlocked = isBlocked,
                Warnings = fileWarnings
            };

            // 상태 설정
            if (isBlocked)
            {
                var blockedFile = validationResult?.BlockedFiles.FirstOrDefault(b => b.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                item.StatusText = blockedFile?.Message ?? "Blocked";
                item.IconKind = "AlertCircle";
                item.StatusBrush = FindResource("ErrorBrush") as SolidColorBrush ?? Brushes.Red;
            }
            else if (fileWarnings.Any())
            {
                item.StatusText = fileWarnings.First().Message;
                item.IconKind = "AlertOutline";
                item.StatusBrush = FindResource("WarningBrush") as SolidColorBrush ?? Brushes.Orange;
            }
            else
            {
                item.IconKind = "FileOutline";
                item.StatusBrush = FindResource("SuccessBrush") as SolidColorBrush ?? Brushes.Green;
            }

            _files.Add(item);
        }

        FilesList.ItemsSource = _files;
    }

    private void UpdateUI()
    {
        var allowedFiles = _files.Where(f => !f.IsBlocked).ToList();
        var blockedFiles = _files.Where(f => f.IsBlocked).ToList();
        var totalSize = allowedFiles.Sum(f => f.Size);

        FileCountText.Text = allowedFiles.Count.ToString();
        TotalSizeText.Text = FormatFileSize(totalSize);
        DeleteModeText.Text = _isPermanent
            ? LanguageService.Instance.GetString("Delete.ModePermanent")
            : LanguageService.Instance.GetString("Delete.ModeTrash");
        DeleteModeText.Foreground = _isPermanent
            ? (FindResource("ErrorBrush") as SolidColorBrush ?? Brushes.Red)
            : (FindResource("WarningBrush") as SolidColorBrush ?? Brushes.Orange);

        // 차단된 파일 표시
        if (blockedFiles.Any())
        {
            BlockedCountText.Text = $"({blockedFiles.Count} {LanguageService.Instance.GetString("Delete.Blocked")})";
            BlockedCountText.Visibility = Visibility.Visible;
        }

        // 경고 섹션 업데이트
        var allWarnings = _files.SelectMany(f => f.Warnings).ToList();
        if (allWarnings.Any())
        {
            WarningsSection.Visibility = Visibility.Visible;
            var warningMessages = allWarnings
                .GroupBy(w => w.Type)
                .Select(g => $"{g.Count()} {GetWarningTypeText(g.Key)}")
                .ToList();
            WarningsText.Text = string.Join(", ", warningMessages);
        }

        // 버튼 텍스트
        ConfirmButton.Content = _isPermanent
            ? LanguageService.Instance.GetString("Delete.ConfirmPermanent")
            : LanguageService.Instance.GetString("Delete.ConfirmTrash");
    }

    private static string GetWarningTypeText(WarningType type)
    {
        var lang = LanguageService.Instance;
        return type switch
        {
            WarningType.HiddenFile => lang.GetString("Delete.WarnHiddenFiles"),
            WarningType.ProtectedExtension => lang.GetString("Delete.WarnProtectedExt"),
            WarningType.LargeFile => lang.GetString("Delete.WarnLargeFiles"),
            WarningType.RecentlyModified => lang.GetString("Delete.WarnRecentlyModified"),
            _ => lang.GetString("Delete.Warnings")
        };
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

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// 삭제 대상 파일 아이템
/// </summary>
public class DeleteFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsBlocked { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string IconKind { get; set; } = "FileOutline";
    public SolidColorBrush StatusBrush { get; set; } = Brushes.Gray;
    public List<FileWarning> Warnings { get; set; } = new();

    public string FormattedSize => FormatFileSize(Size);
    public Visibility HasStatus => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;

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
