using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.NoPayn.Infrastructure;

public class RouteProvider : IRouteProvider
{
    public int Priority => 0;

    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: "NoPayn.PaymentReturn",
            pattern: "NoPayn/PaymentReturn",
            defaults: new { controller = "NoPaynPayment", action = "PaymentReturn" });

        endpointRouteBuilder.MapControllerRoute(
            name: "NoPayn.PaymentCancel",
            pattern: "NoPayn/PaymentCancel",
            defaults: new { controller = "NoPaynPayment", action = "PaymentCancel" });

        endpointRouteBuilder.MapControllerRoute(
            name: "NoPayn.Webhook",
            pattern: "NoPayn/Webhook",
            defaults: new { controller = "NoPaynPayment", action = "Webhook" });
    }
}
