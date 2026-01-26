using DupSweep.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace DupSweep.Infrastructure.Logging;

/// <summary>
/// Serilog 기반 로깅 시스템 설정 및 초기화를 담당합니다.
/// </summary>
public static class LoggingSetup
{
    /// <summary>
    /// 로깅 서비스를 DI 컨테이너에 등록합니다.
    /// </summary>
    public static IServiceCollection AddAppLogging(
        this IServiceCollection services,
        LoggingConfiguration? configuration = null)
    {
        var config = configuration ?? new LoggingConfiguration();

        // 로그 디렉토리 생성
        EnsureLogDirectoryExists(config.LogDirectory);

        // Serilog 로거 구성
        var loggerConfig = CreateLoggerConfiguration(config);
        Log.Logger = loggerConfig.CreateLogger();

        // Microsoft.Extensions.Logging 통합
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        // 구성 등록
        services.AddSingleton(config);

        // AppLogger 등록
        services.AddSingleton<IAppLogger, AppLogger>();

        return services;
    }

    /// <summary>
    /// Serilog LoggerConfiguration을 생성합니다.
    /// </summary>
    private static LoggerConfiguration CreateLoggerConfiguration(LoggingConfiguration config)
    {
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ConvertLogLevel(config.MinimumLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "DupSweep")
            .Enrich.WithProperty("Version", GetApplicationVersion());

        // 스레드 정보 추가
        if (config.IncludeThreadInfo)
        {
            loggerConfig = loggerConfig.Enrich.WithThreadId();
        }

        // 머신 정보 추가
        if (config.IncludeMachineInfo)
        {
            loggerConfig = loggerConfig
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentUserName();
        }

        // 콘솔 출력 (디버그용)
        if (config.EnableConsoleLogging)
        {
            loggerConfig = loggerConfig.WriteTo.Console(
                outputTemplate: config.ConsoleOutputTemplate,
                restrictedToMinimumLevel: ConvertLogLevel(config.MinimumLevel));
        }

        // 파일 로깅 (텍스트)
        if (config.EnableFileLogging)
        {
            var textLogPath = Path.Combine(config.LogDirectory, config.TextLogFilePattern);
            loggerConfig = loggerConfig.WriteTo.File(
                path: textLogPath,
                outputTemplate: config.FileOutputTemplate,
                rollingInterval: ConvertRollingInterval(config.RollingInterval),
                fileSizeLimitBytes: config.FileSizeLimitBytes,
                retainedFileCountLimit: config.RetainedFileCountLimit,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1));
        }

        // 구조화된 로깅 (JSON)
        if (config.EnableStructuredLogging)
        {
            var jsonLogPath = Path.Combine(config.LogDirectory, config.StructuredLogFilePattern);
            loggerConfig = loggerConfig.WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: jsonLogPath,
                rollingInterval: ConvertRollingInterval(config.RollingInterval),
                fileSizeLimitBytes: config.FileSizeLimitBytes,
                retainedFileCountLimit: config.RetainedFileCountLimit,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1));
        }

        // 삭제 전용 로그
        if (config.EnableDeletionLog)
        {
            var deletionLogPath = Path.Combine(config.LogDirectory, config.DeletionLogFilePattern);
            loggerConfig = loggerConfig.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e =>
                    e.Properties.ContainsKey("Operation") &&
                    e.Properties["Operation"].ToString().Contains("Deletion"))
                .WriteTo.File(
                    path: deletionLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: ConvertRollingInterval(config.RollingInterval),
                    fileSizeLimitBytes: config.FileSizeLimitBytes,
                    retainedFileCountLimit: config.RetainedFileCountLimit * 2, // 삭제 로그는 더 오래 보관
                    shared: true));
        }

        return loggerConfig;
    }

    /// <summary>
    /// 로그 디렉토리가 존재하는지 확인하고, 없으면 생성합니다.
    /// </summary>
    private static void EnsureLogDirectoryExists(string logDirectory)
    {
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
    }

    /// <summary>
    /// 애플리케이션 버전을 가져옵니다.
    /// </summary>
    private static string GetApplicationVersion()
    {
        var assembly = typeof(LoggingSetup).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }

    /// <summary>
    /// Core LogLevel을 Serilog LogEventLevel로 변환합니다.
    /// </summary>
    private static LogEventLevel ConvertLogLevel(Core.Logging.LogLevel level)
    {
        return level switch
        {
            Core.Logging.LogLevel.Verbose => LogEventLevel.Verbose,
            Core.Logging.LogLevel.Debug => LogEventLevel.Debug,
            Core.Logging.LogLevel.Information => LogEventLevel.Information,
            Core.Logging.LogLevel.Warning => LogEventLevel.Warning,
            Core.Logging.LogLevel.Error => LogEventLevel.Error,
            Core.Logging.LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Core RollingInterval을 Serilog RollingInterval로 변환합니다.
    /// </summary>
    private static Serilog.RollingInterval ConvertRollingInterval(Core.Logging.RollingInterval interval)
    {
        return interval switch
        {
            Core.Logging.RollingInterval.Infinite => Serilog.RollingInterval.Infinite,
            Core.Logging.RollingInterval.Year => Serilog.RollingInterval.Year,
            Core.Logging.RollingInterval.Month => Serilog.RollingInterval.Month,
            Core.Logging.RollingInterval.Day => Serilog.RollingInterval.Day,
            Core.Logging.RollingInterval.Hour => Serilog.RollingInterval.Hour,
            Core.Logging.RollingInterval.Minute => Serilog.RollingInterval.Minute,
            _ => Serilog.RollingInterval.Day
        };
    }

    /// <summary>
    /// 애플리케이션 종료 시 로거를 정리합니다.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
