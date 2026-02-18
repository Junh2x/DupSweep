using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DupSweep.App.Services;

namespace DupSweep.App.Services;

/// <summary>
/// 알림 서비스.
/// 토스트 스타일 알림을 관리합니다.
/// </summary>
public partial class NotificationService : ObservableObject
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new NotificationService();

    [ObservableProperty]
    private ObservableCollection<NotificationItem> _notifications = new();

    private readonly Dispatcher _dispatcher;
    private readonly int _maxNotifications = 5;
    private readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(5);

    public NotificationService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// 성공 알림을 표시합니다.
    /// </summary>
    public void ShowSuccess(string message, string? title = null, TimeSpan? duration = null)
    {
        ShowNotification(new NotificationItem
        {
            Type = NotificationType.Success,
            Title = title ?? LanguageService.Instance.GetString("Notification.Success"),
            Message = message,
            Duration = duration ?? _defaultDuration
        });
    }

    /// <summary>
    /// 정보 알림을 표시합니다.
    /// </summary>
    public void ShowInfo(string message, string? title = null, TimeSpan? duration = null)
    {
        ShowNotification(new NotificationItem
        {
            Type = NotificationType.Info,
            Title = title ?? LanguageService.Instance.GetString("Notification.Information"),
            Message = message,
            Duration = duration ?? _defaultDuration
        });
    }

    /// <summary>
    /// 경고 알림을 표시합니다.
    /// </summary>
    public void ShowWarning(string message, string? title = null, TimeSpan? duration = null)
    {
        ShowNotification(new NotificationItem
        {
            Type = NotificationType.Warning,
            Title = title ?? LanguageService.Instance.GetString("Notification.Warning"),
            Message = message,
            Duration = duration ?? TimeSpan.FromSeconds(7)
        });
    }

    /// <summary>
    /// 오류 알림을 표시합니다.
    /// </summary>
    public void ShowError(string message, string? title = null, TimeSpan? duration = null)
    {
        ShowNotification(new NotificationItem
        {
            Type = NotificationType.Error,
            Title = title ?? LanguageService.Instance.GetString("Notification.Error"),
            Message = message,
            Duration = duration ?? TimeSpan.FromSeconds(10)
        });
    }

    /// <summary>
    /// 진행 알림을 표시합니다 (자동으로 사라지지 않음).
    /// </summary>
    public NotificationItem ShowProgress(string message, string? title = null)
    {
        var notification = new NotificationItem
        {
            Type = NotificationType.Progress,
            Title = title ?? LanguageService.Instance.GetString("Notification.Processing"),
            Message = message,
            Duration = TimeSpan.MaxValue,
            IsProgress = true
        };

        ShowNotification(notification);
        return notification;
    }

    /// <summary>
    /// 알림을 표시합니다.
    /// </summary>
    public void ShowNotification(NotificationItem notification)
    {
        _dispatcher.Invoke(() =>
        {
            // 최대 개수 초과 시 가장 오래된 알림 제거
            while (Notifications.Count >= _maxNotifications)
            {
                Notifications.RemoveAt(0);
            }

            notification.CreatedAt = DateTime.Now;
            Notifications.Add(notification);

            // 자동 제거 타이머 설정
            if (notification.Duration != TimeSpan.MaxValue && !notification.IsProgress)
            {
                var timer = new DispatcherTimer
                {
                    Interval = notification.Duration
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    DismissNotification(notification);
                };
                timer.Start();
            }
        });
    }

    /// <summary>
    /// 알림을 닫습니다.
    /// </summary>
    public void DismissNotification(NotificationItem notification)
    {
        _dispatcher.Invoke(() =>
        {
            notification.IsClosing = true;

            // 페이드 아웃 후 제거
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Notifications.Remove(notification);
            };
            timer.Start();
        });
    }

    /// <summary>
    /// 모든 알림을 닫습니다.
    /// </summary>
    public void DismissAll()
    {
        _dispatcher.Invoke(() =>
        {
            Notifications.Clear();
        });
    }
}

/// <summary>
/// 알림 타입
/// </summary>
public enum NotificationType
{
    Success,
    Info,
    Warning,
    Error,
    Progress
}

/// <summary>
/// 알림 아이템
/// </summary>
public partial class NotificationItem : ObservableObject
{
    [ObservableProperty]
    private NotificationType _type;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private bool _isProgress;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private bool _isClosing;

    /// <summary>
    /// 아이콘 종류
    /// </summary>
    public string IconKind => Type switch
    {
        NotificationType.Success => "CheckCircle",
        NotificationType.Info => "InformationCircle",
        NotificationType.Warning => "AlertCircle",
        NotificationType.Error => "CloseCircle",
        NotificationType.Progress => "ProgressClock",
        _ => "InformationCircle"
    };

    /// <summary>
    /// 배경색
    /// </summary>
    public string BackgroundColor => Type switch
    {
        NotificationType.Success => "#27AE60",
        NotificationType.Info => "#3498DB",
        NotificationType.Warning => "#F39C12",
        NotificationType.Error => "#E74C3C",
        NotificationType.Progress => "#3498DB",
        _ => "#3498DB"
    };

    /// <summary>
    /// 진행률을 업데이트합니다.
    /// </summary>
    public void UpdateProgress(int progress, string? message = null)
    {
        Progress = progress;
        if (message != null)
        {
            Message = message;
        }
    }
}
