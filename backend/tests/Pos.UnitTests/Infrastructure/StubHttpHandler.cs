using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Pos.Infrastructure.Odoo;

namespace Pos.UnitTests.Infrastructure;

/// <summary>Doble de prueba del transporte HTTP: registra la petición y devuelve una respuesta preparada.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }

    public static StubHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return await respond(request);
    }

    public OdooJson2Client CreateClient(int? posConfigId = null) => new(
        new HttpClient(this) { BaseAddress = new Uri("http://odoo.test/") },
        Options(posConfigId));

    public static IOptions<OdooOptions> Options(int? posConfigId = null) => Microsoft.Extensions.Options.Options.Create(new OdooOptions
    {
        BaseUrl = "http://odoo.test",
        Database = "pos_db",
        ApiKey = "clave-de-prueba",
        PosConfigId = posConfigId,
    });
}
