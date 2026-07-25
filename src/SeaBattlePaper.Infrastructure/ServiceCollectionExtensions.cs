using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeaBattlePaper.Application.Matches;
using SeaBattlePaper.Infrastructure.Persistence;

namespace SeaBattlePaper.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("Connection string 'Default' is required.");

        services.AddDbContext<SeaBattleDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ISeaBattleStore, SeaBattleStore>();

        return services;
    }
}
