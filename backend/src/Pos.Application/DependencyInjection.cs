using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Catalog;
using Pos.Application.Sessions;

namespace Pos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchProductsUseCase>();
        services.AddScoped<GetActiveSessionUseCase>();
        return services;
    }
}
