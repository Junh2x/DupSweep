using System.Windows;
using System.Windows.Input;

namespace DupSweep.App.Services;

/// <summary>
/// 키보드 단축키 서비스.
/// 전역 단축키 바인딩을 관리합니다.
/// </summary>
public class KeyboardShortcutService
{
    private readonly Dictionary<KeyGesture, Action> _shortcuts = new();
    private Window? _targetWindow;

    /// <summary>
    /// 단축키를 등록합니다.
    /// </summary>
    public void RegisterShortcut(Key key, ModifierKeys modifiers, Action action, string description = "")
    {
        var gesture = new KeyGesture(key, modifiers);
        _shortcuts[gesture] = action;
    }

    /// <summary>
    /// 윈도우에 키 이벤트를 연결합니다.
    /// </summary>
    public void AttachToWindow(Window window)
    {
        _targetWindow = window;
        window.PreviewKeyDown += Window_PreviewKeyDown;
    }

    /// <summary>
    /// 윈도우에서 키 이벤트 연결을 해제합니다.
    /// </summary>
    public void DetachFromWindow(Window window)
    {
        window.PreviewKeyDown -= Window_PreviewKeyDown;
        _targetWindow = null;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        foreach (var shortcut in _shortcuts)
        {
            if (shortcut.Key.Key == key && shortcut.Key.Modifiers == modifiers)
            {
                shortcut.Value.Invoke();
                e.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    /// 기본 단축키를 설정합니다.
    /// </summary>
    public void SetupDefaultShortcuts(
        Action navigateHome,
        Action navigateScan,
        Action navigateResults,
        Action navigateSettings,
        Action? startScan = null,
        Action? openHelp = null)
    {
        // 네비게이션 단축키
        RegisterShortcut(Key.D1, ModifierKeys.Control, navigateHome, "Go to Home");
        RegisterShortcut(Key.D2, ModifierKeys.Control, navigateScan, "Go to Scan");
        RegisterShortcut(Key.D3, ModifierKeys.Control, navigateResults, "Go to Results");
        RegisterShortcut(Key.D4, ModifierKeys.Control, navigateSettings, "Go to Settings");

        // 기능 단축키
        if (startScan != null)
        {
            RegisterShortcut(Key.Enter, ModifierKeys.Control, startScan, "Start Scan");
        }

        if (openHelp != null)
        {
            RegisterShortcut(Key.F1, ModifierKeys.None, openHelp, "Open Help");
        }

        // 일반 단축키
        RegisterShortcut(Key.N, ModifierKeys.Control, navigateHome, "New Scan");
    }

    /// <summary>
    /// 등록된 모든 단축키 목록을 반환합니다.
    /// </summary>
    public IEnumerable<ShortcutInfo> GetAllShortcuts()
    {
        return _shortcuts.Select(s => new ShortcutInfo
        {
            Key = s.Key.Key,
            Modifiers = s.Key.Modifiers,
            DisplayString = s.Key.DisplayString
        });
    }
}

/// <summary>
/// 단축키 정보
/// </summary>
public class ShortcutInfo
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public string DisplayString { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
