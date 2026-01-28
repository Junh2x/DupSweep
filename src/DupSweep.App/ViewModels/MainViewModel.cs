using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace DupSweep.App.ViewModels;

/// <summary>
/// 메인 윈도우 ViewModel.
/// 네비게이션 및 현재 뷰 상태를 관리합니다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedNavIndex;

    [ObservableProperty]
    private string _title = "홈";

    private readonly string[] _titles = { "홈", "스캔", "결과", "설정", "폴더 트리" };

    public MainViewModel()
    {
        // 홈 뷰로 시작
        NavigateToHome();
    }

    /// <summary>
    /// 인덱스로 네비게이션
    /// </summary>
    public void NavigateByIndex(int index)
    {
        switch (index)
        {
            case 0:
                NavigateToHome();
                break;
            case 1:
                NavigateToScan();
                break;
            case 2:
                NavigateToResults();
                break;
            case 3:
                NavigateToSettings();
                break;
            case 4:
                NavigateToFolderTree();
                break;
        }
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentView = App.Services.GetRequiredService<HomeViewModel>();
        SelectedNavIndex = 0;
        Title = _titles[0];
    }

    [RelayCommand]
    private void NavigateToScan()
    {
        CurrentView = App.Services.GetRequiredService<ScanViewModel>();
        SelectedNavIndex = 1;
        Title = _titles[1];
    }

    [RelayCommand]
    private void NavigateToResults()
    {
        CurrentView = App.Services.GetRequiredService<ResultsViewModel>();
        SelectedNavIndex = 2;
        Title = _titles[2];
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = App.Services.GetRequiredService<SettingsViewModel>();
        SelectedNavIndex = 3;
        Title = _titles[3];
    }

    [RelayCommand]
    private void NavigateToFolderTree()
    {
        CurrentView = App.Services.GetRequiredService<FolderTreeViewModel>();
        SelectedNavIndex = 4;
        Title = _titles[4];
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        // 이미 NavigateByIndex에서 처리되므로 여기서는 추가 로직 불필요
    }
}
