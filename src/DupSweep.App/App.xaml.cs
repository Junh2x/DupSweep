using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DupSweep.App.ViewModels;
using DupSweep.App.Views;
using DupSweep.Core.Processors;
using DupSweep.Core.Services;
using DupSweep.Core.Services.Interfaces;
using DupSweep.Infrastructure.DependencyInjection;

namespace DupSweep.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ScanViewModel>();
        services.AddTransient<ResultsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Core services
        services.AddSingleton<IScanService, ScanService>();
        services.AddSingleton<IImageProcessor, ImageProcessor>();
        services.AddSingleton<IVideoProcessor, VideoProcessor>();
        services.AddSingleton<IAudioProcessor, AudioProcessor>();

        // Infrastructure services
        services.AddDupSweepInfrastructure();
    }
}
