using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace DupSweep.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private int _selectedNavIndex;

    [ObservableProperty]
    private string _title = "DupSweep";

    public MainViewModel()
    {
        // Start with Home view
        NavigateToHome();
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentView = App.Services.GetRequiredService<HomeViewModel>();
        SelectedNavIndex = 0;
    }

    [RelayCommand]
    private void NavigateToScan()
    {
        CurrentView = App.Services.GetRequiredService<ScanViewModel>();
        SelectedNavIndex = 1;
    }

    [RelayCommand]
    private void NavigateToResults()
    {
        CurrentView = App.Services.GetRequiredService<ResultsViewModel>();
        SelectedNavIndex = 2;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = App.Services.GetRequiredService<SettingsViewModel>();
        SelectedNavIndex = 3;
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        switch (value)
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
        }
    }
}
