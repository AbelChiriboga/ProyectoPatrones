namespace Pos.Domain.Sales;

/// <summary>Ciclo de vida de la venta. En la semana 3 se agregarán los estados de registro en Odoo.</summary>
public enum SaleStatus
{
    Draft,
    Confirmed,
}