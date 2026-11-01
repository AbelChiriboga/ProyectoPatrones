using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.UnitTests.Domain;

public sealed class DiscountPolicyTests
{
    private static DiscountPolicyRegistry CreateRegistry() => new(
    [
        new NoDiscountPolicy(),
        new PercentageDiscountPolicy("promo-10", "Promoción 10 %", 10m),
    ]);

    [Fact]
    public void No_discount_policy_discounts_nothing()
    {
        Assert.Equal(0m, new NoDiscountPolicy().Calculate(50m));
    }

    [Fact]
    public void Percentage_policy_rounds_to_cents()
    {
        var policy = new PercentageDiscountPolicy("promo-10", "Promoción 10 %", 10m);
        Assert.Equal(0.68m, policy.Calculate(6.75m));
    }

    [Fact]
    public void Threshold_policy_applies_only_from_the_minimum_subtotal()
    {
        var policy = new ThresholdPercentageDiscountPolicy("bulk-15", "15 % desde 20 USD", 20m, 15m);

        Assert.Equal(0m, policy.Calculate(19.99m));
        Assert.Equal(3.00m, policy.Calculate(20m));
    }

    [Fact]
    public void Registry_uses_no_discount_when_the_code_is_blank()
    {
        Assert.Equal("none", CreateRegistry().Get("  ").Code);
        Assert.Equal("none", CreateRegistry().Get(null).Code);
    }

    [Fact]
    public void Registry_finds_policies_ignoring_case()
    {
        Assert.Equal("promo-10", CreateRegistry().Get("PROMO-10").Code);
    }

    [Fact]
    public void Registry_rejects_unknown_codes()
    {
        var error = Assert.Throws<DomainException>(() => CreateRegistry().Get("vip-100"));
        Assert.Equal("discount.not_authorized", error.Code);
    }

    [Fact]
    public void Registry_lists_all_policies()
    {
        Assert.Equal(2, CreateRegistry().All.Count);
    }
}
