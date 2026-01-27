using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App;

/// <summary>
/// MainWindow의 코드 비하인드.
/// 커스텀 윈도우 컨트롤 및 네비게이션을 처리합니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;
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
