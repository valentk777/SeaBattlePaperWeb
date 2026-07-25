using Microsoft.Extensions.DependencyInjection;
using SeaBattlePaper.Application.Matches;

namespace SeaBattlePaper.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<SeaBattleService>();

        return services;
    }
}
