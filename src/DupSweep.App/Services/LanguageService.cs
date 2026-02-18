using System.Windows;

namespace DupSweep.App.Services;

/// <summary>
/// 다국어 지원 서비스.
/// ResourceDictionary 기반으로 런타임 언어 전환을 관리합니다.
/// </summary>
public class LanguageService
{
    private static LanguageService? _instance;
    public static LanguageService Instance => _instance ??= new LanguageService();

    private const string LanguageDictionaryPrefix = "Languages/Strings.";
    private const string LanguageDictionaryExtension = ".xaml";

    private AppLanguage _currentLanguage = AppLanguage.Korean;
    private ResourceDictionary? _currentLanguageDictionary;

    /// <summary>
    /// 언어 변경 시 발생하는 이벤트
    /// </summary>
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// 현재 설정된 언어
    /// </summary>
    public AppLanguage CurrentLanguage => _currentLanguage;

    /// <summary>
    /// 초기 언어를 설정하고 리소스를 로드합니다.
    /// App.xaml.cs의 OnStartup에서 호출되어야 합니다.
    /// </summary>
    public void Initialize(AppLanguage language = AppLanguage.Korean)
    {
        _currentLanguage = language;
        LoadLanguageDictionary(language);
    }

    /// <summary>
    /// 언어를 변경합니다. UI에 즉시 반영됩니다.
    /// </summary>
    public void SetLanguage(AppLanguage language)
    {
        if (_currentLanguage == language)
            return;

        _currentLanguage = language;
        LoadLanguageDictionary(language);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 리소스 키로 현재 언어의 문자열을 가져옵니다.
    /// XAML에서 DynamicResource를 사용할 수 없는 ViewModel/Code-behind에서 사용합니다.
    /// </summary>
    public string GetString(string key)
    {
        var value = Application.Current.TryFindResource(key);
        return value as string ?? $"[{key}]";
    }

    /// <summary>
    /// 리소스 키로 문자열을 가져온 뒤 형식 인자를 적용합니다.
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private void LoadLanguageDictionary(AppLanguage language)
    {
        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

        // 기존 언어 사전 제거
        if (_currentLanguageDictionary != null)
        {
            mergedDictionaries.Remove(_currentLanguageDictionary);
        }

        // 새 언어 사전 로드
        var languageCode = language switch
        {
            AppLanguage.Korean => "ko",
            AppLanguage.English => "en",
            _ => "ko"
        };

        var uri = new Uri($"{LanguageDictionaryPrefix}{languageCode}{LanguageDictionaryExtension}", UriKind.Relative);
        var dictionary = new ResourceDictionary { Source = uri };

        mergedDictionaries.Add(dictionary);
        _currentLanguageDictionary = dictionary;
    }
}

/// <summary>
/// 지원 언어 열거형
/// </summary>
public enum AppLanguage
{
    Korean,
    English
}
