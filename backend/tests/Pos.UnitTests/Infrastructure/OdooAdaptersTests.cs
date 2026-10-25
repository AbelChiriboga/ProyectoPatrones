using System.Net;
using Pos.Application.Common;
using Pos.Domain.Sessions;
using Pos.Infrastructure.Odoo;

namespace Pos.UnitTests.Infrastructure;

public sealed class OdooAdaptersTests
{
    private const string ProductsJson = """
        [
          {"id": 11, "name": "Café americano", "default_code": "CAFE-001", "lst_price": 1.5, "taxes_id": [1], "is_storable": false, "qty_available": 0.0},
          {"id": 20, "name": "Botella de agua", "default_code": false, "lst_price": 0.75, "taxes_id": [], "is_storable": true, "qty_available": 42.0}
        ]
        """;

    [Fact]
    public async Task Product_catalog_calls_json2_with_credentials_and_maps_rows()
    {
        var http = StubHttpHandler.Json(ProductsJson);
        var catalog = new OdooProductCatalog(http.CreateClient());

        var products = await catalog.SearchAsync("caf", 10, CancellationToken.None);

        Assert.Equal("http://odoo.test/json/2/product.product/search_read", http.LastRequest!.RequestUri!.ToString());
        Assert.Equal("bearer", http.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("clave-de-prueba", http.LastRequest.Headers.Authorization.Parameter);
        Assert.Equal("pos_db", http.LastRequest.Headers.GetValues("X-Odoo-Database").Single());
        Assert.NotNull(http.LastRequest.Content!.Headers.ContentLength); // Odoo descarta cuerpos chunked
        Assert.Contains("\"available_in_pos\",\"=\",true", http.LastBody);
        Assert.Contains("\"name\",\"ilike\",\"caf\"", http.LastBody);

        Assert.Collection(products,
            cafe =>
            {
                Assert.Equal("CAFE-001", cafe.Code);
                Assert.Null(cafe.QuantityAvailable); // no inventariable: sin dato fiable de existencias
            },
            agua =>
            {
                Assert.Null(agua.Code); // Odoo envía false para campos vacíos
                Assert.Equal(42m, agua.QuantityAvailable);
            });
    }

    [Fact]
    public void Domain_without_term_only_filters_sellable_pos_products()
    {
        var domain = OdooProductCatalog.BuildDomain(null);
        Assert.Equal(2, domain.Length);
    }

    [Fact]
    public async Task Session_gateway_maps_open_session_and_filters_by_configured_pos()
    {
        var http = StubHttpHandler.Json("""
            [{"id": 2, "name": "Caja Principal/00002", "state": "opened", "config_id": [2, "Caja Principal"],
              "currency_id": [1, "USD"], "payment_method_ids": [3]}]
            """);
        var gateway = new OdooPosSessionGateway(http.CreateClient(posConfigId: 2), StubHttpHandler.Options(posConfigId: 2));

        var session = await gateway.FindActiveAsync(CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal(PosSessionState.Opened, session.State);
        Assert.Equal(2, session.ConfigId);
        Assert.Equal("USD", session.CurrencyCode);
        Assert.Contains("\"config_id\",\"=\",2", http.LastBody);
    }

    [Fact]
    public async Task Session_gateway_returns_null_when_odoo_has_no_open_session()
    {
        var http = StubHttpHandler.Json("[]");
        var gateway = new OdooPosSessionGateway(http.CreateClient(), StubHttpHandler.Options());

        Assert.Null(await gateway.FindActiveAsync(CancellationToken.None));
    }

    // Base de P05: un timeout de Odoo se informa como resultado incierto, nunca como éxito.
    [Fact]
    public async Task Timeout_is_reported_as_uncertain_integration_failure()
    {
        var http = new StubHttpHandler(_ => throw new TaskCanceledException("timeout"));
        var catalog = new OdooProductCatalog(http.CreateClient());

        var error = await Assert.ThrowsAsync<IntegrationException>(() => catalog.SearchAsync(null, 5, CancellationToken.None));
        Assert.Equal(IntegrationFailureKind.Timeout, error.Kind);
    }

    [Fact]
    public async Task Connection_failure_is_reported_as_unavailable()
    {
        var http = new StubHttpHandler(_ => throw new HttpRequestException("connection refused"));
        var catalog = new OdooProductCatalog(http.CreateClient());

        var error = await Assert.ThrowsAsync<IntegrationException>(() => catalog.SearchAsync(null, 5, CancellationToken.None));
        Assert.Equal(IntegrationFailureKind.Unavailable, error.Kind);
    }

    [Fact]
    public async Task Rejected_request_keeps_odoo_message()
    {
        var http = StubHttpHandler.Json("""{"name": "werkzeug.exceptions.Unauthorized", "message": "Invalid apikey"}""", HttpStatusCode.Unauthorized);
        var catalog = new OdooProductCatalog(http.CreateClient());

        var error = await Assert.ThrowsAsync<IntegrationException>(() => catalog.SearchAsync(null, 5, CancellationToken.None));
        Assert.Equal(IntegrationFailureKind.Rejected, error.Kind);
        Assert.Contains("Invalid apikey", error.Message);
    }
}
