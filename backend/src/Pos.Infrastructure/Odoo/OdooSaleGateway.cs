using System.Text.Json;
using Microsoft.Extensions.Options;
using Pos.Application.Common;
using Pos.Application.Sales;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Odoo;

/// <summary>Only the explicitly configured POS is accessible. Unsupported fiscal settings fail closed.</summary>
public sealed class OdooSaleGateway(OdooJson2Client client, IOptions<OdooOptions> options) : ISaleGateway
{
    private int ConfigId => options.Value.PosConfigId ?? throw new ValidationException("sales.scope_required", "Configure Odoo:PosConfigId para habilitar ventas.");
    private async Task<JsonElement[]> Read(string model, object[] domain, string[] fields, CancellationToken ct, int limit = 100, string order = "id desc") {
        var json = await client.CallAsync(model, "search_read", new { domain, fields, limit, order }, ct);
        if (json.ValueKind != JsonValueKind.Array) throw Invalid();
        return json.EnumerateArray().ToArray();
    }
    private static IntegrationException Invalid() => new(IntegrationFailureKind.InvalidResponse, "Respuesta POS incompleta o inconsistente.");
    private static int Id(JsonElement row, string name) => row.GetProperty(name)[0].GetInt32();
    private object[] Scope() => [new object[] { "config_id", "=", ConfigId }];
    public async Task<CheckoutDto> QuoteAsync(SalePreviewDto preview, int sessionId, CancellationToken ct) {
        var session = (await Read("pos.session", [new object[]{"id","=",sessionId}, new object[]{"config_id","=",ConfigId}, new object[]{"state","=","opened"}], ["currency_id","payment_method_ids"], ct)).SingleOrDefault();
        if (session.ValueKind == JsonValueKind.Undefined) throw new DomainException("session.not_open", "La sesiÃ³n ya no estÃ¡ abierta.");
        var config = (await Read("pos.config", [new object[]{"id","=",ConfigId}], ["pricelist_id","fiscal_position_ids","company_id","cash_rounding"], ct)).Single();
        // Only base catalog pricing and no fiscal-position mapping are supported in this delivery.
        if (config.GetProperty("cash_rounding").GetBoolean() || config.GetProperty("pricelist_id").ValueKind == JsonValueKind.Array || config.GetProperty("fiscal_position_ids").GetArrayLength() != 0)
            throw new DomainException("tax.unsupported_config", "Esta caja usa lista de precios o posiciones fiscales no soportadas.");
        var currency = (await Read("res.currency", [new object[]{"id","=",Id(session,"currency_id")}], ["name","rounding"], ct)).Single();
        if (currency.GetProperty("rounding").GetDecimal() != 0.01m)
            throw new DomainException("tax.currency", "Solo se admite moneda con precisiÃ³n de centavos.");
        var methods = await Read("pos.payment.method", [new object[]{"id","in",session.GetProperty("payment_method_ids").EnumerateArray().Select(v=>v.GetInt32()).ToArray()}, new object[]{"type","=","cash"}], ["id","name"], ct);
        var products = await Read("product.product", [new object[]{"id","in",preview.Lines.Select(l=>l.ProductId).ToArray()}, new object[]{"sale_ok","=",true}, new object[]{"available_in_pos","=",true}], ["id","taxes_id"], ct);
        var company = (await Read("res.company", [new object[]{"id","=",Id(config,"company_id")}], ["tax_calculation_rounding_method"], ct)).Single();
        var roundingMode = company.GetProperty("tax_calculation_rounding_method").GetString();
        if (roundingMode is not ("round_globally" or "round_per_line")) throw Invalid();
        var taxAccumulators = new Dictionary<int,decimal>();
        decimal allocatedDiscount = 0, cumulativeSubtotal = 0;
        var lines = new List<CheckoutLine>();
        foreach (var line in preview.Lines) {
            var product = products.SingleOrDefault(p=>p.GetProperty("id").GetInt32()==line.ProductId);
            if (product.ValueKind == JsonValueKind.Undefined) throw new DomainException("products.disabled", "Producto retirado del POS.");
            var taxIds = product.GetProperty("taxes_id").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var taxes = taxIds.Length == 0 ? [] : await Read("account.tax", [new object[]{"id","in",taxIds}], ["amount_type","amount","price_include","include_base_amount"], ct);
            if (taxes.Length != taxIds.Length || taxes.Any(t=>t.GetProperty("amount_type").GetString()!="percent" || t.GetProperty("price_include").GetBoolean() || t.GetProperty("include_base_amount").GetBoolean()) || taxes.Length > 1)
                throw new DomainException("tax.unsupported", "Impuestos no soportados: se requiere como mÃ¡ximo un porcentaje excluido por producto.");
            if (Round(line.UnitPrice) != line.UnitPrice) throw new DomainException("price.precision", "Precio con precisión no soportada.");
            cumulativeSubtotal += line.Subtotal;
            var cumulativeDiscount = preview.Subtotal == 0 ? 0 : Round(preview.Discount * cumulativeSubtotal / preview.Subtotal);
            var allocation = cumulativeDiscount - allocatedDiscount;
            allocatedDiscount = cumulativeDiscount;
            var percent = line.Subtotal == 0 ? 0 : allocation / line.Subtotal * 100m;
            var net = Round(line.Subtotal - allocation);
            var tax = AllocateTax(net, taxes.Sum(t=>t.GetProperty("amount").GetDecimal()), taxIds.FirstOrDefault(), roundingMode == "round_globally", taxAccumulators);
            lines.Add(new(line.ProductId,line.Quantity,line.UnitPrice,percent,net,tax,taxIds));
        }
        return new(sessionId,currency.GetProperty("name").GetString()!,lines.Sum(l=>l.Net+l.Tax),lines,
            methods.Select(m=>new PaymentMethodDto(m.GetProperty("id").GetInt32(),m.GetProperty("name").GetString()!)).ToArray());
    }
    internal static decimal AllocateTax(decimal net, decimal rate, int taxId, bool global, Dictionary<int,decimal> accumulated) {
        var raw = net * rate / 100m;
        if (!global) return Round(raw);
        var previous = accumulated.GetValueOrDefault(taxId);
        accumulated[taxId] = previous + raw;
        return Round(previous + raw) - Round(previous);
    }
    private static int Quantity(decimal value) {
        if (value != decimal.Truncate(value) || value <= 0) throw Invalid();
        return checked((int)value);
    }
    private static decimal Round(decimal value) => Math.Round(value,2,MidpointRounding.AwayFromZero);
    public async Task SendAsync(Guid uuid, CheckoutDto quote, int methodId, CancellationToken ct) {
        // No retry policy: timeout and any post-send failure require reconciliation.
        var order = new {
            uuid=uuid.ToString(), session_id=quote.SessionId, state="paid", date_order=DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            amount_tax=quote.Lines.Sum(l=>l.Tax), amount_total=quote.Total, amount_paid=quote.Total, amount_return=0,
            lines=quote.Lines.Select(l=>new object[]{0,0,new {uuid=Guid.NewGuid().ToString(),product_id=l.ProductId,qty=l.Quantity,price_unit=l.UnitPrice,discount=l.DiscountPercent,price_subtotal=l.Net,price_subtotal_incl=l.Net+l.Tax,tax_ids=new object[]{new object[]{6,0,l.TaxIds}}}}).ToArray(),
            payment_ids=new object[]{new object[]{0,0,new {uuid=Guid.NewGuid().ToString(),payment_method_id=methodId,amount=quote.Total,payment_date=DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}}}
        };
        await client.CallAsync("pos.order","sync_from_ui",new { orders=new[]{order} },ct);
    }
    public async Task<SaleDetail?> FindAsync(Guid uuid, CancellationToken ct) {
        var rows = await Orders([..Scope(),new object[]{"uuid","=",uuid.ToString()}],ct,2);
        if (rows.Length > 1) throw Invalid();
        return rows.Length == 0 ? null : await Map(rows[0],ct);
    }
    public async Task<SaleDetail?> DetailAsync(int id, CancellationToken ct) {
        var rows=await Orders([..Scope(),new object[]{"id","=",id}],ct,1);
        return rows.Length==0?null:await Map(rows[0],ct);
    }
    public async Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId,int limit,CancellationToken ct) {
        var domain=Scope().ToList();
        if(beforeId>0) domain.Add(new object[]{"id","<",beforeId});
        var rows=await Orders(domain.ToArray(),ct,limit);
        var results=new List<SaleDetail>();
        foreach(var row in rows) results.Add(await Map(row,ct));
        return results;
    }
    private Task<JsonElement[]> Orders(object[] domain,CancellationToken ct,int limit) => Read("pos.order",domain,["id","uuid","name","state","amount_total","amount_tax","amount_paid","session_id","currency_id","lines","payment_ids"],ct,limit);
    private async Task<SaleDetail> Map(JsonElement row,CancellationToken ct) {
        try { return await MapCore(row,ct); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException) { throw Invalid(); }
    }
    private async Task<SaleDetail> MapCore(JsonElement row,CancellationToken ct) {
        var id=row.GetProperty("id").GetInt32();
        var lines=await Read("pos.order.line",[new object[]{"order_id","=",id}],["product_id","qty","price_unit","discount","price_subtotal","price_subtotal_incl","tax_ids"],ct);
        var payments=await Read("pos.payment",[new object[]{"pos_order_id","=",id}],["payment_method_id","amount"],ct);
        var total=row.GetProperty("amount_total").GetDecimal();
        if (Round(lines.Sum(l=>l.GetProperty("price_subtotal").GetDecimal()) + row.GetProperty("amount_tax").GetDecimal())!=Round(total) ||
            Round(payments.Sum(p=>p.GetProperty("amount").GetDecimal()))!=Round(row.GetProperty("amount_paid").GetDecimal())) throw Invalid();
        return new(id,Guid.Parse(row.GetProperty("uuid").GetString()!),row.GetProperty("name").GetString()!,row.GetProperty("state").GetString()!,row.GetProperty("currency_id")[1].GetString()!,total,row.GetProperty("amount_tax").GetDecimal(),row.GetProperty("amount_paid").GetDecimal(),Id(row,"session_id"),
            lines.Select(l=>new CheckoutLine(Id(l,"product_id"),Quantity(l.GetProperty("qty").GetDecimal()),l.GetProperty("price_unit").GetDecimal(),l.GetProperty("discount").GetDecimal(),l.GetProperty("price_subtotal").GetDecimal(),l.GetProperty("price_subtotal_incl").GetDecimal()-l.GetProperty("price_subtotal").GetDecimal(),l.GetProperty("tax_ids").EnumerateArray().Select(v=>v.GetInt32()).ToArray())).ToArray(),
            payments.Select(p=>new PaymentMethodDto(Id(p,"payment_method_id"),p.GetProperty("payment_method_id")[1].GetString()!)).ToArray());
    }
}
