using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pos.Application.Catalog;
using Pos.Application.Sessions;
using Pos.Infrastructure.Odoo;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OdooOptions>()
            .Bind(configuration.GetSection(OdooOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<OdooJson2Client>((provider, client) =>
        {
            var odoo = provider.GetRequiredService<IOptions<OdooOptions>>().Value;
            client.BaseAddress = new Uri(odoo.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(odoo.TimeoutSeconds);
        });

        services.AddScoped<IProductCatalog, OdooProductCatalog>();
        services.AddScoped<IPosSessionGateway, OdooPosSessionGateway>();
        return services;
    }
}
