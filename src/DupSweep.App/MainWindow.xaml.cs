using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.Services;
using DupSweep.App.ViewModels;

namespace DupSweep.App;

/// <summary>
/// MainWindow의 코드 비하인드.
/// 커스텀 윈도우 컨트롤 및 네비게이션을 처리합니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;
    private readonly KeyboardShortcutService _shortcutService;
    private readonly string[] _subtitles =
    {
        "Select folders to scan for duplicate files",
        "Scanning for duplicate files...",
        "Review and manage duplicate files",
        "Configure application settings"
    };

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = (DataContext as MainViewModel)!;
        _shortcutService = new KeyboardShortcutService();

        // 키보드 단축키 설정
        SetupKeyboardShortcuts();

        // 뷰모델 변경 시 서브타이틀 업데이트
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentView))
                {
                    UpdateSubtitle();
                }
            };
        }

        // 윈도우 로드 시 단축키 서비스 연결
        Loaded += (s, e) => _shortcutService.AttachToWindow(this);
        Unloaded += (s, e) => _shortcutService.DetachFromWindow(this);
    }

    #region Keyboard Shortcuts

    private void SetupKeyboardShortcuts()
    {
        _shortcutService.SetupDefaultShortcuts(
            navigateHome: () => NavigateToIndex(0),
            navigateScan: () => NavigateToIndex(1),
            navigateResults: () => NavigateToIndex(2),
            navigateSettings: () => NavigateToIndex(3),
            openHelp: ShowHelp
        );

        // ESC 키로 스캔 취소
        _shortcutService.RegisterShortcut(Key.Escape, ModifierKeys.None, () =>
        {
            if (_viewModel?.CurrentView is ViewModels.ScanViewModel scanVm && scanVm.IsScanning)
            {
                scanVm.CancelScanCommand?.Execute(null);
                NotificationService.Instance.ShowWarning("Scan cancelled", "Cancelled");
            }
        }, "Cancel Scan");

        // Ctrl+Delete로 선택된 중복 파일 휴지통으로 이동
        _shortcutService.RegisterShortcut(Key.Delete, ModifierKeys.Control, () =>
        {
            if (_viewModel?.CurrentView is ViewModels.ResultsViewModel resultsVm && resultsVm.SelectedFilesCount > 0)
            {
                resultsVm.MoveToTrashCommand?.Execute(null);
            }
        }, "Move Selected to Trash");

        // Shift+Delete로 영구 삭제
        _shortcutService.RegisterShortcut(Key.Delete, ModifierKeys.Shift, () =>
        {
            if (_viewModel?.CurrentView is ViewModels.ResultsViewModel resultsVm && resultsVm.SelectedFilesCount > 0)
            {
                resultsVm.DeletePermanentlyCommand?.Execute(null);
            }
        }, "Delete Permanently");
    }

    private void NavigateToIndex(int index)
    {
        _viewModel?.NavigateByIndex(index);
        UpdateNavRadioButton(index);
        UpdateSubtitle(index);
    }

    private void UpdateNavRadioButton(int index)
    {
        RadioButton? navButton = index switch
        {
            0 => HomeNav,
            1 => ScanNav,
            2 => ResultsNav,
            3 => SettingsNav,
            _ => null
        };

        if (navButton != null)
        {
            navButton.IsChecked = true;
        }
    }

    private void ShowHelp()
    {
        NotificationService.Instance.ShowInfo(
            "Ctrl+1~4: Navigate pages\n" +
            "Ctrl+N: Go to Home\n" +
            "Ctrl+Del: Move to trash\n" +
            "Shift+Del: Delete permanently\n" +
            "Esc: Cancel scan\n" +
            "F1: This help",
            "Keyboard Shortcuts",
            TimeSpan.FromSeconds(8));
    }

    #endregion

    #region Window Controls

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeRestore();
        }
        else
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MinimizeButton_Click(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        MaximizeRestore();
    }

    private void MaximizeButton_Click(object sender, MouseButtonEventArgs e)
    {
        MaximizeRestore();
    }

    private void MaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion

    #region Navigation

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.Tag is string tagStr)
        {
            if (int.TryParse(tagStr, out int index))
            {
                _viewModel?.NavigateByIndex(index);
                UpdateSubtitle(index);
            }
        }
    }

    private void UpdateSubtitle(int? index = null)
    {
        if (index == null)
        {
            index = _viewModel?.SelectedNavIndex ?? 0;
        }

        if (index >= 0 && index < _subtitles.Length)
        {
            PageSubtitle.Text = _subtitles[index.Value];
        }
    }

    #endregion

    #region Results Badge

    public void UpdateResultsBadge(int count)
    {
        if (count > 0)
        {
            ResultsBadge.Visibility = Visibility.Visible;
            ResultsBadgeText.Text = count > 99 ? "99+" : count.ToString();
        }
        else
        {
            ResultsBadge.Visibility = Visibility.Collapsed;
        }
    }

    #endregion
}
