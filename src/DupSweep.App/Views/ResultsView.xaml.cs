using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Views;

/// <summary>
/// 결과 화면 코드비하인드
/// 파일 행 클릭: 선택 토글 + 상세 패널 표시
/// 해시 열 가시성: ShowHashColumn 변경 시 DataGrid 열 Visibility 토글
/// </summary>
public partial class ResultsView : UserControl
{
    private const int HashColumnIndex = 7; // DataGrid 마지막 열 (해시)

    public ResultsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ResultsViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is ResultsViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateHashColumnVisibility(newVm.ShowHashColumn);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResultsViewModel.ShowHashColumn) && sender is ResultsViewModel vm)
        {
            UpdateHashColumnVisibility(vm.ShowHashColumn);
        }
    }

    private void UpdateHashColumnVisibility(bool show)
    {
        if (FileListView.Columns.Count > HashColumnIndex)
        {
            FileListView.Columns[HashColumnIndex].Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void DataGridClip_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Grid grid)
        {
            grid.Clip = new RectangleGeometry(
                new Rect(0, 0, grid.ActualWidth, grid.ActualHeight),
                11, 11);
        }
    }

    private void FileRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element)
        {
            var fileVm = FindFileItemViewModel(element);
            if (fileVm == null)
            {
                return;
            }

            fileVm.IsSelected = !fileVm.IsSelected;

            if (DataContext is ResultsViewModel vm)
            {
                vm.FocusedFile = (vm.FocusedFile == fileVm) ? null : fileVm;
            }
        }
    }

    private static FileItemViewModel? FindFileItemViewModel(FrameworkElement element)
    {
        DependencyObject? current = element;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is FileItemViewModel fileVm)
            {
                return fileVm;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
