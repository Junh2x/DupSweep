using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DupSweep.App.ViewModels;

namespace DupSweep.App.Views;

/// <summary>
/// 결과 화면 코드비하인드
/// 파일 카드 클릭 이벤트 처리
/// </summary>
public partial class ResultsView : UserControl
{
    public ResultsView()
    {
        InitializeComponent();
    }

    private void FileCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is FileItemViewModel fileVm)
        {
            fileVm.IsSelected = !fileVm.IsSelected;
        }
    }
}
