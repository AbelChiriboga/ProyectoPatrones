using Pos.Application.Common;
using Pos.Application.Sessions;
using Pos.Domain.Common;
using Pos.Domain.Sessions;

namespace Pos.UnitTests.Application;

public sealed class GetActiveSessionUseCaseTests
{
    private sealed class FakeSessionGateway(PosSession? session) : IPosSessionGateway
    {
        public Task<PosSession?> FindActiveAsync(CancellationToken cancellationToken) => Task.FromResult(session);
    }

    private static PosSession Session(PosSessionState state) =>
        new(2, "Caja Principal/00002", state, 2, "USD", [3]);

    [Fact]
    public async Task Returns_open_session()
    {
        var useCase = new GetActiveSessionUseCase(new FakeSessionGateway(Session(PosSessionState.Opened)));

        var result = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, result.Id);
        Assert.Equal("Opened", result.State);
        Assert.Equal([3], result.PaymentMethodIds);
    }

    [Fact]
    public async Task Fails_when_no_session_is_open()
    {
        var useCase = new GetActiveSessionUseCase(new FakeSessionGateway(null));

        var error = await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(CancellationToken.None));
        Assert.Equal("session.none_open", error.Code);
    }

    // Base de P03: una sesión cerrada (simulada mediante el contrato externo) no admite ventas.
    [Theory]
    [InlineData(PosSessionState.Closed)]
    [InlineData(PosSessionState.ClosingControl)]
    [InlineData(PosSessionState.OpeningControl)]
    public async Task Rejects_session_that_does_not_accept_sales(PosSessionState state)
    {
        var useCase = new GetActiveSessionUseCase(new FakeSessionGateway(Session(state)));

        var error = await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(CancellationToken.None));
        Assert.Equal("session.not_open", error.Code);
    }
}
