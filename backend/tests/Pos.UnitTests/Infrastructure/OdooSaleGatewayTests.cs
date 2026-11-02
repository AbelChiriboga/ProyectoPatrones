using Pos.Infrastructure.Odoo;
using Pos.Application.Common;
using Pos.Application.Sales;
namespace Pos.UnitTests.Infrastructure;
public sealed class OdooSaleGatewayTests {
    [Fact] public void Global_tax_rounding_matches_company_group_not_sum_of_rounded_lines() {
        var accumulator=new Dictionary<int,decimal>();
        var a=OdooSaleGateway.AllocateTax(1.5m,15m,1,true,accumulator);
        var b=OdooSaleGateway.AllocateTax(0.9m,15m,1,true,accumulator);
        Assert.Equal(0.36m,a+b);
        Assert.Equal(0.37m,OdooSaleGateway.AllocateTax(1.5m,15m,1,false,[])+OdooSaleGateway.AllocateTax(0.9m,15m,1,false,[]));
    }
    [Fact] public async Task History_requires_explicit_scope() {
        var handler=StubHttpHandler.Json("[]");
        var gateway=new OdooSaleGateway(handler.CreateClient(),StubHttpHandler.Options());
        await Assert.ThrowsAsync<ValidationException>(()=>gateway.HistoryAsync(0,20,default));
        Assert.Null(handler.LastRequest);
    }
    [Fact] public async Task Detail_query_is_limited_to_configured_pos() {
        var handler=StubHttpHandler.Json("[]");
        var gateway=new OdooSaleGateway(handler.CreateClient(8),StubHttpHandler.Options(8));
        Assert.Null(await gateway.DetailAsync(2,default));
        Assert.Contains("config_id",handler.LastBody);Assert.Contains("8",handler.LastBody);
    }
    [Fact] public async Task Unexpected_odoo_shape_is_not_a_sale() {
        var handler=StubHttpHandler.Json("{}");
        var gateway=new OdooSaleGateway(handler.CreateClient(1),StubHttpHandler.Options(1));
        await Assert.ThrowsAsync<IntegrationException>(()=>gateway.FindAsync(Guid.NewGuid(),default));
    }
}
