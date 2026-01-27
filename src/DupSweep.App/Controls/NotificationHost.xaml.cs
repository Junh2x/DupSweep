using System.Windows;
using System.Windows.Controls;
using DupSweep.App.Services;

namespace DupSweep.App.Controls;

/// <summary>
/// NotificationHost.xaml에 대한 상호 작용 논리.
/// 토스트 알림을 호스팅하는 컨트롤입니다.
/// </summary>
public partial class NotificationHost : UserControl
{
    public NotificationHost()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 닫기 버튼 클릭 이벤트 핸들러.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NotificationItem notification)
        {
            NotificationService.Instance.DismissNotification(notification);
        }
    }
}
