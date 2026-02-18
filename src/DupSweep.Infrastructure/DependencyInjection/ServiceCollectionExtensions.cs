using DupSweep.Core.Logging;
using DupSweep.Core.Models;
using DupSweep.Core.Services.Interfaces;
using DupSweep.Infrastructure.FileSystem;
using DupSweep.Infrastructure.Hashing;
using DupSweep.Infrastructure.Logging;
using DupSweep.Infrastructure.Parallel;
using Microsoft.Extensions.DependencyInjection;

namespace DupSweep.Infrastructure.DependencyInjection;

/// <summary>
/// DI 컨테이너 확장 메서드
/// Infrastructure 레이어의 모든 서비스를 등록
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// DupSweep Infrastructure 서비스를 DI 컨테이너에 등록합니다.
    /// </summary>
    public static IServiceCollection AddDupSweepInfrastructure(this IServiceCollection services)
    {
        // 로깅 시스템
        services.AddAppLogging();

        // 안전 삭제 옵션 (기본값)
        services.AddSingleton(SafeDeleteOptions.Default);

        // 삭제 검증 서비스
        services.AddSingleton<IDeleteValidationService, DeleteValidationService>();

        // 해시 서비스
        services.AddSingleton<IHashService, HashService>();

        // 파일 삭제 서비스
        services.AddSingleton<IDeleteService, DeleteService>();

        // 병렬 처리 옵션 및 실행기
        services.AddSingleton(ParallelProcessingOptions.Default);
        services.AddSingleton<IParallelExecutor, ParallelExecutor>();

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

        // 안전 삭제 옵션 (기본값)
        services.AddSingleton(SafeDeleteOptions.Default);

        // 삭제 검증 서비스
        services.AddSingleton<IDeleteValidationService, DeleteValidationService>();

        // 해시 서비스
        services.AddSingleton<IHashService, HashService>();

        // 파일 삭제 서비스
        services.AddSingleton<IDeleteService, DeleteService>();

        // 병렬 처리 옵션 및 실행기
        services.AddSingleton(ParallelProcessingOptions.Default);
        services.AddSingleton<IParallelExecutor, ParallelExecutor>();

        return services;
    }

    /// <summary>
    /// 사용자 정의 로깅 및 삭제 옵션을 사용하여 Infrastructure 서비스를 등록합니다.
    /// </summary>
    public static IServiceCollection AddDupSweepInfrastructure(
        this IServiceCollection services,
        LoggingConfiguration loggingConfig,
        SafeDeleteOptions deleteOptions)
    {
        // 로깅 시스템 (사용자 정의 구성)
        services.AddAppLogging(loggingConfig);

        // 안전 삭제 옵션 (사용자 정의)
        services.AddSingleton(deleteOptions);

        // 삭제 검증 서비스
        services.AddSingleton<IDeleteValidationService, DeleteValidationService>();

        // 해시 서비스
        services.AddSingleton<IHashService, HashService>();

        // 파일 삭제 서비스
        services.AddSingleton<IDeleteService, DeleteService>();

        // 병렬 처리 옵션 및 실행기
        services.AddSingleton(ParallelProcessingOptions.Default);
        services.AddSingleton<IParallelExecutor, ParallelExecutor>();

        return services;
    }
}
