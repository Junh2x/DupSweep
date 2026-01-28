using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.Services;
using DupSweep.App.ViewModels;

namespace DupSweep.App;

/// <summary>
/// MainWindow 코드 비하인드.
/// 커스텀 윈도우 컨트롤과 네비게이션을 처리합니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly KeyboardShortcutService _shortcutService;
    private readonly string[] _subtitles =
    {
        "중복 파일을 검색할 폴더를 선택하세요",
        "중복 파일을 검색 중...",
        "중복 파일을 검토하고 관리하세요",
        "애플리케이션 설정",
        "폴더 구조를 탐색하고 용량을 확인하세요"
    };

        public MainWindow()
    {
        InitializeComponent();

        _shortcutService = new KeyboardShortcutService();
        DataContextChanged += MainWindow_DataContextChanged;

        // 키보드 단축키 설정
        SetupKeyboardShortcuts();

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

        // ESC로 스캔 취소
        _shortcutService.RegisterShortcut(Key.Escape, ModifierKeys.None, () =>
        {
            if (ViewModel?.CurrentView is ViewModels.ScanViewModel scanVm && scanVm.IsScanning)
            {
                scanVm.CancelScanCommand?.Execute(null);
                NotificationService.Instance.ShowWarning("스캔이 취소되었습니다", "취소됨");
            }
        }, "스캔 취소");

        // Ctrl+Delete로 선택한 중복 파일 휴지통으로 이동
        _shortcutService.RegisterShortcut(Key.Delete, ModifierKeys.Control, () =>
        {
            if (ViewModel?.CurrentView is ViewModels.ResultsViewModel resultsVm && resultsVm.SelectedFilesCount > 0)
            {
                resultsVm.MoveToTrashCommand?.Execute(null);
            }
        }, "휴지통으로 이동");

        // Shift+Delete로 영구 삭제
        _shortcutService.RegisterShortcut(Key.Delete, ModifierKeys.Shift, () =>
        {
            if (ViewModel?.CurrentView is ViewModels.ResultsViewModel resultsVm && resultsVm.SelectedFilesCount > 0)
            {
                resultsVm.DeletePermanentlyCommand?.Execute(null);
            }
        }, "영구 삭제");
    }

    private void NavigateToIndex(int index)
    {
        ViewModel?.NavigateByIndex(index);
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
            4 => FolderTreeNav,
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
            "Ctrl+1~5: 페이지 이동\n" +
            "Ctrl+N: 홈으로 이동\n" +
            "Ctrl+Del: 휴지통으로 이동\n" +
            "Shift+Del: 영구 삭제\n" +
            "Esc: 스캔 취소\n" +
            "F1: 도움말",
            "키보드 단축키",
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
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
                ViewModel?.NavigateByIndex(index);
                UpdateSubtitle(index);
            }
        }
    }

    private void UpdateSubtitle(int? index = null)
    {
        if (index == null)
        {
            index = ViewModel?.SelectedNavIndex ?? 0;
        }

        if (index >= 0 && index < _subtitles.Length)
        {
            PageSubtitle.Text = _subtitles[index.Value];
        }
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }

        UpdateSubtitle();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentView))
        {
            UpdateSubtitle();
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedNavIndex))
        {
            // 메뉴 탭 동기화: ViewModel에서 SelectedNavIndex가 변경되면 RadioButton 업데이트
            UpdateNavRadioButton(ViewModel?.SelectedNavIndex ?? 0);
            UpdateSubtitle(ViewModel?.SelectedNavIndex);
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
