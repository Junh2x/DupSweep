using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Controls;

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
