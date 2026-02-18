using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App;

/// <summary>
/// MainWindow 코드 비하인드.
/// 커스텀 윈도우 컨트롤과 네비게이션을 처리합니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly string[] _subtitles =
    {
        "중복 파일을 검색할 폴더를 선택하세요",
        "중복 파일을 검색 중...",
        "", // (미사용)
        "애플리케이션 설정",
        "폴더 구조를 탐색하고 용량을 확인하세요"
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
    }

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

    private void UpdateNavRadioButton(int index)
    {
        RadioButton? navButton = index switch
        {
            0 => HomeNav,
            1 => ScanNav,
            3 => SettingsNav,
            4 => FolderTreeNav,
            _ => null
        };

        if (navButton != null)
        {
            navButton.IsChecked = true;
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
            UpdateNavRadioButton(ViewModel?.SelectedNavIndex ?? 0);
            UpdateSubtitle(ViewModel?.SelectedNavIndex);
        }
    }
    #endregion
}
