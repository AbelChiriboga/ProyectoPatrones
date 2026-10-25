using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Pos.Application.Common;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Cliente de la API externa JSON-2 de Odoo 20 (POST /json/2/{modelo}/{método}).
/// Concentra autenticación, serialización y traducción de fallos HTTP a <see cref="IntegrationException"/>,
/// de modo que los adaptadores solo expresan qué consultar.
/// </summary>
public sealed class OdooJson2Client(HttpClient http, IOptions<OdooOptions> options)
{
    private readonly OdooOptions _options = options.Value;

    public async Task<JsonElement> CallAsync(string model, string method, object arguments, CancellationToken cancellationToken)
    {
        // Se envía con Content-Length explícito: Odoo ignora los cuerpos con transferencia "chunked"
        // (que es lo que produce JsonContent), y la llamada se ejecutaría sin argumentos.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"json/2/{model}/{method}")
        {
            Content = new StringContent(JsonSerializer.Serialize(arguments), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", _options.ApiKey);
        request.Headers.Add("X-Odoo-Database", _options.Database);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IntegrationException(IntegrationFailureKind.Timeout,
                $"Odoo no respondió a tiempo ({model}.{method}); el resultado es incierto.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new IntegrationException(IntegrationFailureKind.Unavailable,
                $"No fue posible comunicarse con Odoo ({model}.{method}).", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new IntegrationException(KindFor(response.StatusCode), DescribeError(model, method, response.StatusCode, body));

            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new IntegrationException(IntegrationFailureKind.InvalidResponse,
                    $"Odoo devolvió una respuesta que no es JSON ({model}.{method}).", ex);
            }
        }
    }

    private static IntegrationFailureKind KindFor(HttpStatusCode status) =>
        (int)status >= 500
            ? IntegrationFailureKind.Unavailable
            : IntegrationFailureKind.Rejected;

    private static string DescribeError(string model, string method, HttpStatusCode status, string body)
    {
        // JSON-2 devuelve {"name": ..., "message": ...}; se conserva el mensaje sin exponer trazas.
        var detail = "sin detalle";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message))
                detail = message.GetString() ?? detail;
        }
        catch (JsonException)
        {
        }
        return $"Odoo rechazó {model}.{method} (HTTP {(int)status}): {detail}";
    }
}
