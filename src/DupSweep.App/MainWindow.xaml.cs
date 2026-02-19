using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DupSweep.App.ViewModels;

namespace DupSweep.App;

/// <summary>
/// MainWindow 코드 비하인드.
/// 커스텀 윈도우 컨트롤과 네비게이션을 처리합니다.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
        MainBorder.SizeChanged += MainBorder_SizeChanged;
    }

    private void MainBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        MainBorder.Clip = new RectangleGeometry
        {
            RadiusX = 12,
            RadiusY = 12,
            Rect = new Rect(0, 0, MainBorder.ActualWidth, MainBorder.ActualHeight)
        };
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

    private bool _isNavigating;

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;

        if (sender is RadioButton radioButton && radioButton.Tag is string tagStr)
        {
            if (int.TryParse(tagStr, out int index))
            {
                ViewModel?.NavigateByIndex(index);
            }
        }
    }

    private void UpdateNavRadioButton(int index)
    {
        _isNavigating = true;
        try
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
        finally
        {
            _isNavigating = false;
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
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNavIndex))
        {
            UpdateNavRadioButton(ViewModel?.SelectedNavIndex ?? 0);
        }
    }
    #endregion
}
