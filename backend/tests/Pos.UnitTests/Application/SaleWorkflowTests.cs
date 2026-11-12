using Pos.Application.Catalog;
using Pos.Application.Sales;
using Pos.Application.Sessions;
using Pos.Application.Common;
using Pos.Domain.Catalog;
using Pos.Domain.Discounts;
using Pos.Domain.Sessions;
using Pos.Infrastructure.Sales;

namespace Pos.UnitTests.Application;
public sealed class SaleWorkflowTests
{
    private sealed class Catalog : IProductCatalog {
        public bool Enabled = true;
        public Task<IReadOnlyList<Product>> SearchAsync(string? t,int l,CancellationToken c)=>GetByIdsAsync([],c);
        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids,CancellationToken c)=>Task.FromResult<IReadOnlyList<Product>>(Enabled?[new Product(1,"P","Producto",10m,[],null)]:[]);
    }
    private sealed class Sessions : IPosSessionGateway {
        public PosSessionState State = PosSessionState.Opened;
        public Task<PosSession?> FindActiveAsync(CancellationToken c)=>Task.FromResult<PosSession?>(new(1,"Caja",State,1,"USD",[1]));
    }
    private sealed class Gateway : ISaleGateway {
        public int Sends; public bool Timeout; public bool Paid=true; public SaleDetail? Stored;
        public Task<CheckoutDto> QuoteAsync(SalePreviewDto p,int s,CancellationToken c)=>Task.FromResult(new CheckoutDto(s,"USD",p.Total,[new(1,1,10,0,10,0,[])],[new(1,"Efectivo")]));
        public Task SendAsync(Guid id,CheckoutDto q,int m,CancellationToken c) {
            Interlocked.Increment(ref Sends);
            Stored=new(7,id,"POS-7",Paid?"paid":"draft","USD",q.Total,0,Paid?q.Total:0,1,q.Lines,[new(1,"Efectivo")]);
            if(Timeout) throw new IntegrationException(IntegrationFailureKind.Timeout,"timeout");
            return Task.CompletedTask;
        }
        public Task<SaleDetail?> FindAsync(Guid id,CancellationToken c)=>Task.FromResult(Stored);
        public Task<SaleDetail?> DetailAsync(int id,CancellationToken c)=>Task.FromResult(Stored);
        public Task<IReadOnlyList<SaleDetail>> HistoryAsync(int b,int l,CancellationToken c)=>Task.FromResult<IReadOnlyList<SaleDetail>>([]);
    }
    private static RegisterSaleRequest Request()=>new(Guid.NewGuid(),new(1,[new(1,1)],"none"),1,10m);
    private static (SaleWorkflow Flow,Gateway Gateway,Catalog Catalog,Sessions Sessions,string Path) Create() {
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pos-test-"+Guid.NewGuid());
        var gateway=new Gateway();var catalog=new Catalog();var sessions=new Sessions();
        return(new(new(catalog,new([new NoDiscountPolicy()])),sessions,gateway,new FileSaleOperationStore(path)),gateway,catalog,sessions,path);
    }
    [Fact] public async Task Repeated_request_after_store_reopen_sends_once() {
        var x=Create();var r=Request();
        Assert.Equal("confirmed",(await x.Flow.RegisterAsync("a",r,default)).Status);
        var reopened=new SaleWorkflow(new(x.Catalog,new([new NoDiscountPolicy()])),x.Sessions,x.Gateway,new FileSaleOperationStore(x.Path));
        Assert.Equal("confirmed",(await reopened.RegisterAsync("a",r,default)).Status);Assert.Equal(1,x.Gateway.Sends);
    }
    [Fact] public async Task Concurrent_requests_send_once() {
        var x=Create();var r=Request();
        var jobs=Enumerable.Range(0,12).Select(_=>Task.Run(async()=> {try {await x.Flow.RegisterAsync("a",r,default);}catch(SaleConflictException){} }));
        await Task.WhenAll(jobs);Assert.Equal(1,x.Gateway.Sends);
    }
    [Fact] public async Task Timeout_reconciles_without_resending() {
        var x=Create();x.Gateway.Timeout=true;var r=Request();
        Assert.Equal("uncertain",(await x.Flow.RegisterAsync("a",r,default)).Status);
        Assert.Equal("confirmed",(await x.Flow.RegisterAsync("a",r,default)).Status);Assert.Equal(1,x.Gateway.Sends);
    }
    [Fact] public async Task Unpaid_response_never_confirms() {
        var x=Create();x.Gateway.Paid=false;var r=Request();
        Assert.Equal("uncertain",(await x.Flow.RegisterAsync("a",r,default)).Status);
        await x.Flow.RegisterAsync("a",r,default);Assert.Equal(1,x.Gateway.Sends);
    }
    [Fact] public async Task Payload_or_owner_collision_is_rejected() {
        var x=Create();var r=Request();await x.Flow.RegisterAsync("a",r,default);
        await Assert.ThrowsAsync<SaleConflictException>(()=>x.Flow.RegisterAsync("b",r,default));
        await Assert.ThrowsAsync<SaleConflictException>(()=>x.Flow.RegisterAsync("a",r with {ExpectedTotal=9},default));
        await Assert.ThrowsAsync<NotFoundException>(()=>x.Flow.StatusAsync("b",r.OperationId,default));
    }
    [Theory][InlineData(0)][InlineData(-1)][InlineData(100001)]
    public async Task Invalid_quantity_never_sends(int quantity) {
        var x=Create();var r=Request();
        await Assert.ThrowsAsync<ValidationException>(()=>x.Flow.RegisterAsync("a",r with {Sale=r.Sale with {Lines=[new(1,quantity)]}},default));Assert.Equal(0,x.Gateway.Sends);
    }
    [Fact] public async Task Empty_closed_disabled_and_invalid_method_never_send() {
        var x=Create();var r=Request();
        await Assert.ThrowsAsync<Pos.Domain.Common.DomainException>(()=>x.Flow.RegisterAsync("a",r with {Sale=r.Sale with {Lines=[]}},default));
        x.Sessions.State=PosSessionState.Closed;
        await Assert.ThrowsAsync<Pos.Domain.Common.DomainException>(()=>x.Flow.RegisterAsync("a",r,default));
        x.Sessions.State=PosSessionState.Opened;x.Catalog.Enabled=false;
        await Assert.ThrowsAsync<NotFoundException>(()=>x.Flow.RegisterAsync("a",r,default));
        x.Catalog.Enabled=true;
        await Assert.ThrowsAsync<Pos.Domain.Common.DomainException>(()=>x.Flow.RegisterAsync("a",r with {PaymentMethodId=99},default));
        await Assert.ThrowsAsync<Pos.Domain.Common.DomainException>(()=>x.Flow.RegisterAsync("a",r with {Sale=r.Sale with {DiscountCode="hack"}},default));
        Assert.Equal(0,x.Gateway.Sends);
    }
    [Fact] public void Corrupt_claim_blocks_resend() {
        var x=Create();var id=Guid.NewGuid();Directory.CreateDirectory(x.Path);
        File.WriteAllText(System.IO.Path.Combine(x.Path,id+".claim"),"{");
        Assert.Throws<SaleConflictException>(()=>new FileSaleOperationStore(x.Path).Read(id));
    }
}
