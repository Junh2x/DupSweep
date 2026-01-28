using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Controls;

/// <summary>
/// 폴더 트리 시각화 컨트롤 코드비하인드
/// 바 세그먼트 클릭 및 브레드크럼 네비게이션 처리
/// </summary>
public partial class FolderTreeView : UserControl
{
    public FolderTreeView()
    {
        InitializeComponent();
    }

    private FolderTreeViewModel? ViewModel => DataContext as FolderTreeViewModel;

    private void BarSegment_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is FolderBarItem item && ViewModel != null)
        {
            ViewModel.SelectItem(item);
        }
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is BreadcrumbItem crumb && ViewModel != null)
        {
            ViewModel.NavigateToBreadcrumb(crumb);
        }
    }
}
