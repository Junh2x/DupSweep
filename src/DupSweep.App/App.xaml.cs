using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using DupSweep.App.ViewModels;
using DupSweep.App.Views;
using DupSweep.Core.Logging;
using DupSweep.Core.Processors;
using DupSweep.Core.Services;
using DupSweep.Core.Services.Interfaces;
using DupSweep.Infrastructure.DependencyInjection;
using DupSweep.Infrastructure.Logging;

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

        // 애플리케이션 시작 로그
        var logger = Services.GetRequiredService<IAppLogger>();
        logger.LogInformation("DupSweep 애플리케이션 시작");
        logger.LogMemoryUsage("애플리케이션 시작");

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 애플리케이션 종료 로그
        try
        {
            var logger = Services.GetService<IAppLogger>();
            logger?.LogInformation("DupSweep 애플리케이션 종료");
            logger?.LogMemoryUsage("애플리케이션 종료");
        }
        finally
        {
            // Serilog 정리
            LoggingSetup.CloseAndFlush();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 로깅 구성
        var loggingConfig = new LoggingConfiguration
        {
            MinimumLevel = LogLevel.Information,
            EnableFileLogging = true,
            EnableConsoleLogging = false,
            EnableStructuredLogging = true,
            EnableDeletionLog = true,
            EnablePerformanceLogging = true,
            RetainedFileCountLimit = 30
        };

#if DEBUG
        loggingConfig.MinimumLevel = LogLevel.Debug;
        loggingConfig.EnableConsoleLogging = true;
#endif

        // Infrastructure services (로깅 포함)
        services.AddDupSweepInfrastructure(loggingConfig);

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
    }
}
