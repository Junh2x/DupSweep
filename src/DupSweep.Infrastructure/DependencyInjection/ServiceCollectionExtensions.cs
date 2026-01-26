using DupSweep.Core.Services.Interfaces;
using DupSweep.Infrastructure.Caching;
using DupSweep.Infrastructure.FileSystem;
using DupSweep.Infrastructure.Hashing;
using Microsoft.Extensions.DependencyInjection;

namespace DupSweep.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDupSweepInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IDeleteService, DeleteService>();
        services.AddSingleton<IThumbnailCache, ThumbnailCache>();
        services.AddSingleton<IHashCache, HashCache>();

        return services;
    }
}
