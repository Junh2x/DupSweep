using DupSweep.Core.Logging;
using DupSweep.Core.Services.Interfaces;
using DupSweep.Infrastructure.Caching;
using DupSweep.Infrastructure.FileSystem;
using DupSweep.Infrastructure.Hashing;
using DupSweep.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DupSweep.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// DupSweep Infrastructure 서비스를 DI 컨테이너에 등록합니다.
    /// </summary>
    public static IServiceCollection AddDupSweepInfrastructure(this IServiceCollection services)
    {
        // 로깅 시스템
        services.AddAppLogging();

        // 해시 서비스
        services.AddSingleton<IHashService, HashService>();

        // 파일 삭제 서비스
        services.AddSingleton<IDeleteService, DeleteService>();

        // 캐싱 서비스
        services.AddSingleton<IThumbnailCache, ThumbnailCache>();
        services.AddSingleton<IHashCache, HashCache>();

        return services;
    }

    /// <summary>
    /// 사용자 정의 로깅 구성을 사용하여 Infrastructure 서비스를 등록합니다.
    /// </summary>
    public static IServiceCollection AddDupSweepInfrastructure(
        this IServiceCollection services,
        LoggingConfiguration loggingConfig)
    {
        // 로깅 시스템 (사용자 정의 구성)
        services.AddAppLogging(loggingConfig);

        // 해시 서비스
        services.AddSingleton<IHashService, HashService>();

        // 파일 삭제 서비스
        services.AddSingleton<IDeleteService, DeleteService>();

        // 캐싱 서비스
        services.AddSingleton<IThumbnailCache, ThumbnailCache>();
        services.AddSingleton<IHashCache, HashCache>();

        return services;
    }
}
