using System.Windows;
using System.Windows.Controls;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Controls;

/// <summary>
/// 폴더 트리 뷰 컨트롤.
/// 폴더 구조를 탐색하고 용량 정보를 시각화합니다.
/// </summary>
public partial class FolderTreeView : UserControl
{
    public FolderTreeView()
    {
        InitializeComponent();
        Loaded += FolderTreeView_Loaded;
    }

    private FolderTreeViewModel? ViewModel => DataContext as FolderTreeViewModel;

    /// <summary>
    /// 선택된 폴더 경로
    /// </summary>
    public static readonly DependencyProperty SelectedPathProperty =
        DependencyProperty.Register(
            nameof(SelectedPath),
            typeof(string),
            typeof(FolderTreeView),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string SelectedPath
    {
        get => (string)GetValue(SelectedPathProperty);
        set => SetValue(SelectedPathProperty, value);
    }

    /// <summary>
    /// 폴더 선택 이벤트
    /// </summary>
    public event EventHandler<FolderSelectedEventArgs>? FolderSelected;

    private async void FolderTreeView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null && ViewModel.RootNodes.Count == 0)
        {
            await ViewModel.LoadDrivesAsync();
        }
    }

    private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem treeViewItem &&
            treeViewItem.DataContext is FolderNodeViewModel node &&
            ViewModel != null)
        {
            if (!node.IsLoaded && !node.IsLoading)
            {
                await ViewModel.LoadChildrenAsync(node);
            }
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNodeViewModel node)
        {
            SelectedPath = node.FullPath;

            if (ViewModel != null)
            {
                ViewModel.SelectedNode = node;
            }

            FolderSelected?.Invoke(this, new FolderSelectedEventArgs(node.FullPath, node));
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.LoadDrivesAsync();
        }
    }

    /// <summary>
    /// 선택된 폴더의 크기를 계산합니다.
    /// </summary>
    public async Task CalculateSelectedFolderSizeAsync()
    {
        if (ViewModel?.SelectedNode != null)
        {
            await ViewModel.CalculateFolderSizeAsync(ViewModel.SelectedNode);
        }
    }
}

/// <summary>
/// 폴더 선택 이벤트 인자
/// </summary>
public class FolderSelectedEventArgs : EventArgs
{
    public string FolderPath { get; }
    public FolderNodeViewModel Node { get; }

    public FolderSelectedEventArgs(string folderPath, FolderNodeViewModel node)
    {
        FolderPath = folderPath;
        Node = node;
    }
}
