using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Views;

/// <summary>
/// 결과 화면 코드비하인드
/// 파일 행 클릭: 선택 토글 + 상세 패널 표시
/// </summary>
public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
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
