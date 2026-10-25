using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pos.Domain.Sessions;
using Pos.Infrastructure.Odoo;

namespace Pos.IntegrationTests;

/// <summary>Se omite si no hay una instancia de Odoo configurada mediante variables de entorno.</summary>
public sealed class OdooFactAttribute : FactAttribute
{
    public OdooFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Odoo__ApiKey")))
            Skip = "Defina Odoo__ApiKey (y opcionalmente Odoo__BaseUrl, Odoo__Database) para ejecutar contra Odoo real.";
    }
}

/// <summary>Pruebas contra la instancia local de Odoo 20 Community (sin dobles de prueba).</summary>
public sealed class OdooIntegrationTests
{
    private static (OdooJson2Client Client, IOptions<OdooOptions> Options) Connect()
    {
        var settings = new OdooOptions();
        new ConfigurationBuilder().AddEnvironmentVariables().Build().GetSection(OdooOptions.SectionName).Bind(settings);
        if (string.IsNullOrWhiteSpace(settings.Database))
            settings.Database = "pos_db";

        var options = Options.Create(settings);
        var http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds) };
        return (new OdooJson2Client(http, options), options);
    }

    [OdooFact]
    public async Task Reads_sellable_pos_products_from_odoo()
    {
        var (client, _) = Connect();

        var products = await new OdooProductCatalog(client).SearchAsync(null, 50, CancellationToken.None);

        Assert.True(products.Count >= 2, "Odoo debe tener al menos dos productos vendibles en el POS.");
        Assert.All(products, p => Assert.True(p.UnitPrice >= 0));
    }

    [OdooFact]
    public async Task Searches_product_by_internal_code()
    {
        var (client, _) = Connect();

        var products = await new OdooProductCatalog(client).SearchAsync("CAFE-001", 10, CancellationToken.None);

        var product = Assert.Single(products);
        Assert.Equal("CAFE-001", product.Code);
    }

    [OdooFact]
    public async Task Search_without_matches_returns_empty_list()
    {
        var (client, _) = Connect();

        var products = await new OdooProductCatalog(client).SearchAsync("no-existe-xyz", 10, CancellationToken.None);

        Assert.Empty(products);
    }

    [OdooFact]
    public async Task Finds_open_pos_session_with_payment_methods()
    {
        var (client, options) = Connect();

        var session = await new OdooPosSessionGateway(client, options).FindActiveAsync(CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal(PosSessionState.Opened, session.State);
        Assert.NotEmpty(session.PaymentMethodIds);
    }
}
