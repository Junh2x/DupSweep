using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Views;

/// <summary>
/// 홈 화면 코드비하인드
/// 폴더 드래그 앤 드롭 처리
/// </summary>
public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private HomeViewModel? ViewModel => DataContext as HomeViewModel;

    #region Drag and Drop

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(f => Directory.Exists(f)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (Directory.Exists(file))
                    {
                        ViewModel?.AddFolder(file);
                    }
                }
            }
        }
    }

    #endregion
}
