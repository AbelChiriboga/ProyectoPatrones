using System.Net;
using System.Text;
using System.Text.Json;
using Pos.Application.Common;
using Pos.Application.Sales;
using Pos.Infrastructure.Odoo;
namespace Pos.UnitTests.Infrastructure;

public sealed class OdooHistoryReaderTests
{
    private static string Rows => """
      [{"id":10,"name":"Caja 10","date_order":"2026-11-02 03:00:00","amount_total":9.66,"currency_id":[1,"USD"],"state":"paid"},
       {"id":9,"name":"Caja 9","date_order":false,"amount_total":3.2,"currency_id":[1,"USD"],"state":"done"},
       {"id":8,"name":"Caja 8","date_order":false,"amount_total":2,"currency_id":[1,"USD"],"state":"paid"}]
      """;
    [Fact] public async Task One_scoped_header_query_fetches_limit_plus_one_without_lines_or_payments() {
        var calls=0;
        var handler=new StubHttpHandler(request=> {
            calls++; Assert.EndsWith("/pos.order/search_read",request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Rows,Encoding.UTF8,"application/json")});
        });
        var page=await new OdooSaleGateway(handler.CreateClient(3),StubHttpHandler.Options(3)).SearchAsync(new("Caja",20,2),default);
        Assert.Equal(1,calls);Assert.Equal(2,page.Items.Count);Assert.True(page.HasMore);Assert.Equal(9,page.NextBeforeId);
        Assert.Equal(new DateTimeOffset(2026,11,2,3,0,0,TimeSpan.Zero),page.Items[0].DateUtc);Assert.Null(page.Items[1].DateUtc);
        using var body=JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(3,body.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal("id desc",body.RootElement.GetProperty("order").GetString());
        Assert.Contains("config_id",handler.LastBody);Assert.Contains("pos_reference",handler.LastBody);Assert.Contains("ilike",handler.LastBody);
        Assert.DoesNotContain("payment_ids",handler.LastBody);Assert.DoesNotContain("lines",handler.LastBody);
        Assert.Equal(9.66m,page.Items[0].Total);
    }
    [Fact] public async Task No_extra_row_means_no_next_cursor() {
        var handler=StubHttpHandler.Json("[]");
        var page=await new OdooSaleGateway(handler.CreateClient(3),StubHttpHandler.Options(3)).SearchAsync(new("",0,20),default);
        Assert.False(page.HasMore);Assert.Null(page.NextBeforeId);Assert.Empty(page.Items);
    }
    [Fact] public async Task Scope_is_mandatory_for_summary_reads_too() {
        var handler=StubHttpHandler.Json("[]");
        await Assert.ThrowsAsync<ValidationException>(()=>new OdooSaleGateway(handler.CreateClient(),StubHttpHandler.Options()).SearchAsync(new("",0,20),default));
        Assert.Null(handler.LastRequest);
    }
    [Theory][InlineData("{}")][InlineData("[{\"id\":1}]")]
    public async Task Malformed_history_is_not_reported_as_a_valid_page(string json) {
        var handler=StubHttpHandler.Json(json);
        await Assert.ThrowsAsync<IntegrationException>(()=>new OdooSaleGateway(handler.CreateClient(3),StubHttpHandler.Options(3)).SearchAsync(new("",0,20),default));
    }
}
