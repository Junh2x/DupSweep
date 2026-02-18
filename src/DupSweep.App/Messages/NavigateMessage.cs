using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DupSweep.App.Messages;

/// <summary>
/// ViewModel 간 네비게이션을 위한 메시지.
/// WeakReferenceMessenger를 통해 전달되어 순환 의존성 없이 화면 전환을 수행.
/// </summary>
public class NavigateMessage : ValueChangedMessage<NavigationTarget>
{
    public NavigateMessage(NavigationTarget target) : base(target) { }
}

/// <summary>
/// 네비게이션 대상 열거형
/// </summary>
public enum NavigationTarget
{
    Home,
    Scan,
    Results,
    Settings,
    FolderTree
}
