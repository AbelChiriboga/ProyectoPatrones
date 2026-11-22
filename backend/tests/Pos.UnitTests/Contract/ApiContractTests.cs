using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pos.Api.Controllers;
using Pos.Application.Auth;
using Pos.Application.Sales;
using Xunit;

namespace Pos.UnitTests.Contract;

public class ApiContractTests : IClassFixture<PosApiTestFactory>
{
    private readonly PosApiTestFactory _factory;
    private readonly HttpClient _client;

    public ApiContractTests(PosApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string? GetErrorCode(JsonElement root)
    {
        if (root.TryGetProperty("code", out var code))
            return code.GetString();
        if (root.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var extCode))
            return extCode.GetString();
        return null;
    }

    private async Task<string> GetTokenAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        return result.Token;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var token = await GetTokenAsync(username, password);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ==========================================
    // 1. OpenAPI & Swagger UI Contract Tests
    // ==========================================

    [Fact]
    public async Task OpenApi_JsonSpecification_ReturnsContractDefinitionWithBearerAuthAndPaths()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("application/json", response.Content.Headers.ContentType.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.True(root.TryGetProperty("openapi", out var openapiProp));
        Assert.StartsWith("3.", openapiProp.GetString());

        // Validate Info
        var info = root.GetProperty("info");
        Assert.Equal("POS Odoo Community API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());

        // Validate Key Paths (both versioned v1 and legacy unversioned)
        var paths = root.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _), "Missing /api/v1/auth/login path");
        Assert.True(paths.TryGetProperty("/api/auth/login", out _), "Missing /api/auth/login path");
        Assert.True(paths.TryGetProperty("/api/v1/products", out _), "Missing /api/v1/products path");
        Assert.True(paths.TryGetProperty("/api/products", out _), "Missing /api/products path");
        Assert.True(paths.TryGetProperty("/api/v1/discounts", out _), "Missing /api/v1/discounts path");
        Assert.True(paths.TryGetProperty("/api/discounts", out _), "Missing /api/discounts path");
        Assert.True(paths.TryGetProperty("/api/v1/pos/sessions/active", out _), "Missing /api/v1/pos/sessions/active path");
        Assert.True(paths.TryGetProperty("/api/pos/sessions/active", out _), "Missing /api/pos/sessions/active path");
        Assert.True(paths.TryGetProperty("/api/v1/reports/sales", out _), "Missing /api/v1/reports/sales path");
        Assert.True(paths.TryGetProperty("/api/reports/sales", out _), "Missing /api/reports/sales path");
        Assert.True(paths.TryGetProperty("/api/v1/sales/history", out _), "Missing /api/v1/sales/history path");

        // Validate Bearer Security Scheme in components
        var components = root.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString(), ignoreCase: true);
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task SwaggerUI_IndexHtml_ReturnsOkAndContainsSwaggerUiMarkup()
    {
        var response = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Contains("text/html", response.Content.Headers.ContentType.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger", content, StringComparison.OrdinalIgnoreCase);
    }

    // ==========================================
    // 2. Versioning & Header Parity Tests
    // ==========================================

    [Fact]
    public async Task ApiVersioning_ResponseHeaders_ArePresentOnResponses()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/discounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Api-Version"), "Missing X-Api-Version header");
        Assert.True(response.Headers.Contains("api-supported-versions"), "Missing api-supported-versions header");

        Assert.Equal("1.0", response.Headers.GetValues("X-Api-Version").First());
        Assert.Equal("1.0", response.Headers.GetValues("api-supported-versions").First());
    }

    [Fact]
    public async Task ApiVersioning_DualRouteAlias_ReturnsIdenticalContract()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);

        var responseV1 = await authClient.GetAsync("/api/v1/discounts");
        var responseLegacy = await authClient.GetAsync("/api/discounts");

        Assert.Equal(HttpStatusCode.OK, responseV1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseLegacy.StatusCode);

        var contentV1 = await responseV1.Content.ReadAsStringAsync();
        var contentLegacy = await responseLegacy.Content.ReadAsStringAsync();

        Assert.Equal(contentV1, contentLegacy);
        Assert.Equal(responseV1.Headers.GetValues("X-Api-Version").First(), responseLegacy.Headers.GetValues("X-Api-Version").First());
    }

    [Theory]
    [InlineData("99.0")]
    [InlineData("2.0")]
    [InlineData("invalid-version")]
    public async Task ApiVersioning_UnsupportedVersionHeader_Returns400ProblemDetails(string unsupportedVersion)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/discounts");
        request.Headers.Add("X-Api-Version", unsupportedVersion);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("api.unsupported_version", GetErrorCode(root));
    }

    [Fact]
    public async Task Cors_LanOrigin_PreflightRequest_ReturnsAllowedOrigin()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/discounts");
        request.Headers.Add("Origin", "http://192.168.1.100:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await _client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://192.168.1.100:5173", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    // ==========================================
    // 3. Auth & Identity Contract Tests
    // ==========================================

    [Fact]
    public async Task Auth_ValidCashierLogin_ReturnsContractWithTokenAndCashierRole()
    {
        var request = new LoginRequest(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal(PosApiTestFactory.CashierUser, result.Username);
        Assert.Equal(OperatorRoles.Cashier, result.Role);
        Assert.True(result.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Auth_ValidAdminLogin_ReturnsContractWithTokenAndAdminRole()
    {
        var request = new LoginRequest(PosApiTestFactory.AdminUser, PosApiTestFactory.AdminPass);
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal(PosApiTestFactory.AdminUser, result.Username);
        Assert.Equal(OperatorRoles.Admin, result.Role);
    }

    [Fact]
    public async Task Auth_InvalidPassword_Returns401ProblemDetails()
    {
        var request = new LoginRequest(PosApiTestFactory.CashierUser, "wrong-password");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(401, root.GetProperty("status").GetInt32());
        Assert.Equal("auth.invalid_credentials", GetErrorCode(root));
    }

    [Fact]
    public async Task Auth_EmptyCredentials_Returns400ProblemDetails()
    {
        var request = new LoginRequest("", "");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("auth.credentials_required", GetErrorCode(root));
    }

    // ==========================================
    // 4. Policy-Based Authorization & Role Security Contracts
    // ==========================================

    [Fact]
    public async Task SecurityPolicy_UnauthenticatedEndpoint_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecurityPolicy_CashierRole_AccessingAdminReport_Returns403Forbidden()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await cashierClient.GetAsync("/api/v1/reports/sales");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecurityPolicy_AdminRole_AccessingAdminReport_Returns200WithContractSchema()
    {
        var adminClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.AdminUser, PosApiTestFactory.AdminPass);
        var response = await adminClient.GetAsync("/api/v1/reports/sales");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.True(root.TryGetProperty("totalOrders", out var totalOrdersProp) && totalOrdersProp.GetInt32() >= 0);
        Assert.True(root.TryGetProperty("grandTotal", out var grandTotalProp) && grandTotalProp.GetDecimal() >= 0);
        Assert.True(root.TryGetProperty("currency", out var currencyProp) && currencyProp.GetString() == "USD");
        Assert.True(root.TryGetProperty("byCashier", out var byCashierProp) && byCashierProp.ValueKind == JsonValueKind.Array);
        Assert.True(root.TryGetProperty("byPaymentMethod", out var byPaymentProp) && byPaymentProp.ValueKind == JsonValueKind.Array);
    }

    // ==========================================
    // 5. Data Schemas, Field Types & Error Contracts
    // ==========================================

    [Fact]
    public async Task Products_Search_ReturnsArrayOfProductContractObjects()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/products?search=mesa&limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() > 0);

        var first = root[0];
        Assert.True(first.GetProperty("id").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("name").GetString()));
        Assert.True(first.GetProperty("unitPrice").GetDecimal() > 0);
        Assert.Equal(JsonValueKind.Array, first.GetProperty("taxIds").ValueKind);
    }

    [Fact]
    public async Task Products_InvalidLimit_Returns400ProblemDetails()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/products?limit=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("products.invalid_limit", GetErrorCode(root));
    }

    [Fact]
    public async Task PosSessions_Active_ReturnsPosSessionContract()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/pos/sessions/active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(10, root.GetProperty("id").GetInt32());
        Assert.Equal("POS/2026/001", root.GetProperty("name").GetString());
        Assert.Equal("Opened", root.GetProperty("state").GetString());
        Assert.Equal("USD", root.GetProperty("currencyCode").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("paymentMethodIds").ValueKind);
    }

    [Fact]
    public async Task Discounts_List_ReturnsArrayOfDiscountContractObjects()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/discounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.True(root.GetArrayLength() > 0);

        foreach (var item in root.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("name").GetString()));
        }
    }

    [Fact]
    public async Task Sales_Detail_NonExistentId_Returns404ProblemDetails()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/sales/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("sales.not_found", GetErrorCode(root));
    }

    [Fact]
    public async Task Sales_History_ScopedToCashier_Returns200PageContract()
    {
        var authClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var response = await authClient.GetAsync("/api/v1/sales/history?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);

        var root = json.RootElement;
        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.False(root.GetProperty("hasMore").GetBoolean());
    }

    // ==========================================
    // 6. End-to-End POS Sales Flow & Resilience Contracts
    // ==========================================

    [Fact]
    public async Task SaleFlow_Preview_ReturnsProvisionalSubtotalAndDiscount()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var request = new PreviewSaleRequest([new SaleLineRequest(1, 2)], "none");

        var response = await cashierClient.PostAsJsonAsync("/api/v1/sales/preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<SalePreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(240.00m, preview.Subtotal);
        Assert.Equal(0m, preview.Discount);
        Assert.Equal(240.00m, preview.Total);
        Assert.Single(preview.Lines);
        Assert.Equal(2, preview.Lines[0].Quantity);
    }

    [Fact]
    public async Task SaleFlow_Preview_WithPromo10_AppliesDiscountStrategyCorrectly()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var request = new PreviewSaleRequest([new SaleLineRequest(1, 1)], "promo-10");

        var response = await cashierClient.PostAsJsonAsync("/api/v1/sales/preview", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var preview = await response.Content.ReadFromJsonAsync<SalePreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(120.00m, preview.Subtotal);
        Assert.Equal(12.00m, preview.Discount);
        Assert.Equal(108.00m, preview.Total);
    }

    [Fact]
    public async Task SaleFlow_Quote_ReturnsSessionCheckoutWithPaymentMethods()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var request = new CheckoutRequest(10, [new SaleLineRequest(1, 1)], "none");

        var response = await cashierClient.PostAsJsonAsync("/api/v1/sales/quote", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var quote = await response.Content.ReadFromJsonAsync<CheckoutDto>();
        Assert.NotNull(quote);
        Assert.Equal(10, quote.SessionId);
        Assert.Equal(120.00m, quote.Total);
        Assert.NotEmpty(quote.Methods);
        Assert.Equal(1, quote.Methods[0].Id);
    }

    [Fact]
    public async Task SaleFlow_FullEndToEnd_Register_ConfirmsSale_And_RepetitionIsIdempotent()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var operationId = Guid.NewGuid();

        // 1. Quote cart
        var quoteReq = new CheckoutRequest(10, [new SaleLineRequest(1, 1)], "none");
        var quoteRes = await cashierClient.PostAsJsonAsync("/api/v1/sales/quote", quoteReq);
        Assert.Equal(HttpStatusCode.OK, quoteRes.StatusCode);
        var quote = await quoteRes.Content.ReadFromJsonAsync<CheckoutDto>();
        Assert.NotNull(quote);

        // 2. Register sale
        var registerReq = new RegisterSaleRequest(operationId, quoteReq, 1, quote.Total);
        var registerRes = await cashierClient.PostAsJsonAsync("/api/v1/sales", registerReq);
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        var result = await registerRes.Content.ReadFromJsonAsync<OperationResult>();
        Assert.NotNull(result);
        Assert.Equal(operationId, result.OperationId);
        Assert.Equal("confirmed", result.Status);
        Assert.NotNull(result.Sale);
        Assert.Equal("paid", result.Sale.State);
        Assert.Equal(quote.Total, result.Sale.Total);
        Assert.Equal(quote.Total, result.Sale.Paid);

        // 3. Repeated request with same operationId (Idempotency guarantee)
        var repeatRes = await cashierClient.PostAsJsonAsync("/api/v1/sales", registerReq);
        Assert.Equal(HttpStatusCode.OK, repeatRes.StatusCode);

        var repeatResult = await repeatRes.Content.ReadFromJsonAsync<OperationResult>();
        Assert.NotNull(repeatResult);
        Assert.Equal(result.OperationId, repeatResult.OperationId);
        Assert.Equal("confirmed", repeatResult.Status);
        Assert.Equal(result.Sale.Id, repeatResult.Sale?.Id);

        // 4. Reconcile / Status check via GET
        var statusRes = await cashierClient.GetAsync($"/api/v1/sales/operations/{operationId}");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);

        var statusResult = await statusRes.Content.ReadFromJsonAsync<OperationResult>();
        Assert.NotNull(statusResult);
        Assert.Equal(operationId, statusResult.OperationId);
        Assert.Equal("confirmed", statusResult.Status);
        Assert.Equal(result.Sale.Id, statusResult.Sale?.Id);
    }

    [Fact]
    public async Task SaleFlow_Register_AlteredPayloadWithSameOperationId_Returns409Conflict()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var operationId = Guid.NewGuid();

        // First registration
        var req1 = new RegisterSaleRequest(operationId, new CheckoutRequest(10, [new SaleLineRequest(1, 1)], "none"), 1, 120.00m);
        var res1 = await cashierClient.PostAsJsonAsync("/api/v1/sales", req1);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Altered payload with same operationId (e.g. quantity 2 instead of 1)
        var req2 = new RegisterSaleRequest(operationId, new CheckoutRequest(10, [new SaleLineRequest(1, 2)], "none"), 1, 240.00m);
        var res2 = await cashierClient.PostAsJsonAsync("/api/v1/sales", req2);

        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
        var json = await res2.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task SaleFlow_Register_ExpectedTotalMismatch_Returns409TotalChanged()
    {
        var cashierClient = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var operationId = Guid.NewGuid();

        // Pass 999.00m instead of true 120.00m
        var req = new RegisterSaleRequest(operationId, new CheckoutRequest(10, [new SaleLineRequest(1, 1)], "none"), 1, 999.00m);
        var res = await cashierClient.PostAsJsonAsync("/api/v1/sales", req);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("sale.total_changed", GetErrorCode(json.RootElement));
    }

    [Fact]
    public async Task SaleFlow_Reconcile_DifferentCashier_Returns404NotFound()
    {
        var cashier1Client = await CreateAuthenticatedClientAsync(PosApiTestFactory.CashierUser, PosApiTestFactory.CashierPass);
        var cashier2Client = await CreateAuthenticatedClientAsync(PosApiTestFactory.Cashier2User, PosApiTestFactory.Cashier2Pass);
        var operationId = Guid.NewGuid();

        // Cashier 1 registers sale
        var req = new RegisterSaleRequest(operationId, new CheckoutRequest(10, [new SaleLineRequest(1, 1)], "none"), 1, 120.00m);
        var res1 = await cashier1Client.PostAsJsonAsync("/api/v1/sales", req);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Cashier 2 attempts to reconcile Cashier 1's operation
        var res2 = await cashier2Client.GetAsync($"/api/v1/sales/operations/{operationId}");
        Assert.Equal(HttpStatusCode.NotFound, res2.StatusCode);
    }
}
