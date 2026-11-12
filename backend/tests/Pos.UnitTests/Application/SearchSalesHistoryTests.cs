using Pos.Application.Sales;
using Pos.Application.Common;
namespace Pos.UnitTests.Application;

public sealed class SearchSalesHistoryTests
{
    private sealed class Reader : ISaleHistoryReader
    {
        public SaleHistoryQuery? Last;
        public Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct) {
            Last = query;
            return Task.FromResult(new SaleHistoryPage([], false, null));
        }
    }
    [Fact] public async Task Normalizes_search_without_fetching_unbounded_data() {
        var reader = new Reader();
        var page = await new SearchSalesHistoryUseCase(reader).ExecuteAsync(" Caja - 000002 ", 100, 20, default);
        Assert.Equal(new SaleHistoryQuery("Caja - 000002",100,20),reader.Last);
        Assert.Empty(page.Items); Assert.False(page.HasMore);
    }
    [Theory][InlineData(-1,20)][InlineData(0,0)][InlineData(0,101)]
    public async Task Invalid_pagination_never_calls_reader(int cursor,int limit) {
        var reader = new Reader();
        await Assert.ThrowsAsync<ValidationException>(()=>new SearchSalesHistoryUseCase(reader).ExecuteAsync(null,cursor,limit,default));
        Assert.Null(reader.Last);
    }
    [Theory][InlineData("a\n")][InlineData("a\u200b")][InlineData("a\u2028")]
    public async Task Invalid_search_never_calls_reader(string search) {
        var reader = new Reader();
        await Assert.ThrowsAsync<ValidationException>(()=>new SearchSalesHistoryUseCase(reader).ExecuteAsync(search,0,20,default));
        Assert.Null(reader.Last);
    }
    [Fact] public async Task Search_limit_is_checked_before_trimming() {
        var reader = new Reader();
        await Assert.ThrowsAsync<ValidationException>(()=>new SearchSalesHistoryUseCase(reader).ExecuteAsync(new string('x',101),0,20,default));
        Assert.Null(reader.Last);
    }
}
