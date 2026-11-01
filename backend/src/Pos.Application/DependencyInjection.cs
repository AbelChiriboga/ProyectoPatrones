using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Auth;
using Pos.Application.Catalog;
using Pos.Application.Discounts;
using Pos.Application.Sales;
using Pos.Application.Sessions;
using Pos.Domain.Discounts;

namespace Pos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Políticas de descuento autorizadas. Para agregar una nueva (RF-07) basta registrarla aquí.
        services.AddSingleton<IDiscountPolicy, NoDiscountPolicy>();
        services.AddSingleton<IDiscountPolicy>(new PercentageDiscountPolicy("promo-10", "Promoción 10 %", 10m));
        services.AddSingleton<IDiscountPolicy>(new ThresholdPercentageDiscountPolicy("bulk-15", "15 % en compras desde 20 USD", 20m, 15m));
        services.AddSingleton<DiscountPolicyRegistry>();

        services.AddScoped<SearchProductsUseCase>();
        services.AddScoped<GetActiveSessionUseCase>();
        services.AddScoped<PreviewSaleUseCase>();
        services.AddScoped<ListDiscountsUseCase>();
        services.AddScoped<LoginUseCase>();
        return services;
    }
}
