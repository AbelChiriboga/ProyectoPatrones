using Pos.Domain.Catalog;
using Pos.Domain.Common;

namespace Pos.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void Rejects_negative_reference_price()
    {
        var error = Assert.Throws<DomainException>(() => new Product(1, "X", "Producto", -0.01m, [], null));
        Assert.Equal("product.invalid_price", error.Code);
    }

    [Fact]
    public void Rejects_product_without_name()
    {
        Assert.Throws<DomainException>(() => new Product(1, "X", "  ", 1m, [], null));
    }

    [Fact]
    public void Treats_blank_code_as_missing()
    {
        var product = new Product(1, " ", "Producto", 1m, [], null);
        Assert.Null(product.Code);
    }
}
