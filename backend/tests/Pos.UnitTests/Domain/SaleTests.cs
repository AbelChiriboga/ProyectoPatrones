using Pos.Domain.Catalog;
using Pos.Domain.Common;
using Pos.Domain.Sales;

namespace Pos.UnitTests.Domain;

public sealed class SaleTests
{
    private static Product Coffee() => new(11, "CAFE-001", "Café americano", 1.50m, [1], null);
    private static Product Sandwich() => new(12, "SAND-001", "Sánduche de pollo", 3.75m, [1], null);

    [Fact]
    public void Adding_a_product_creates_a_line()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 2);

        var line = Assert.Single(sale.Lines);
        Assert.Equal(11, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void Adding_the_same_product_twice_merges_the_quantity()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);
        sale.AddProduct(Coffee(), 2);

        var line = Assert.Single(sale.Lines);
        Assert.Equal(3, line.Quantity);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_quantity_not_greater_than_zero(int quantity)
    {
        var sale = new Sale();
        var error = Assert.Throws<DomainException>(() => sale.AddProduct(Coffee(), quantity));
        Assert.Equal("sale.invalid_quantity", error.Code);
    }

    [Fact]
    public void Subtotal_is_the_sum_of_the_lines()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 2);
        sale.AddProduct(Sandwich(), 1);

        Assert.Equal(6.75m, sale.Subtotal);
    }

    [Fact]
    public void Changing_the_quantity_updates_the_line()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);
        sale.ChangeQuantity(11, 4);

        Assert.Equal(4, sale.Lines[0].Quantity);
        Assert.Equal(6.00m, sale.Subtotal);
    }


    [Fact]
    public void Changing_the_quantity_to_zero_is_rejected()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);

        var error = Assert.Throws<DomainException>(() => sale.ChangeQuantity(11, 0));
        Assert.Equal("sale.invalid_quantity", error.Code);
    }

    [Fact]
    public void Removing_a_product_removes_its_line()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);
        sale.RemoveProduct(11);

        Assert.Empty(sale.Lines);
    }

    [Fact]
    public void Removing_a_product_that_is_not_in_the_sale_fails()
    {
        var sale = new Sale();
        var error = Assert.Throws<DomainException>(() => sale.RemoveProduct(99));
        Assert.Equal("sale.line_not_found", error.Code);
    }

    [Fact]
    public void Cannot_confirm_an_empty_sale()
    {
        var sale = new Sale();

        var error = Assert.Throws<DomainException>(() => sale.Confirm());
        Assert.Equal("sale.empty", error.Code);
    }

    [Fact]
    public void Confirming_marks_the_sale_as_confirmed()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);
        sale.Confirm();

        Assert.Equal(SaleStatus.Confirmed, sale.Status);
    }

    [Fact]
    public void Confirmed_sale_cannot_be_edited()
    {
        var sale = new Sale();
        sale.AddProduct(Coffee(), 1);
        sale.Confirm();

        Assert.Equal("sale.not_editable", Assert.Throws<DomainException>(() => sale.AddProduct(Sandwich(), 1)).Code);
        Assert.Equal("sale.not_editable", Assert.Throws<DomainException>(() => sale.ChangeQuantity(11, 2)).Code);
        Assert.Equal("sale.not_editable", Assert.Throws<DomainException>(() => sale.RemoveProduct(11)).Code);
    }
}