using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.NoPayn.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.NoPayn.Components;

public class PaymentInfoViewComponent : NopViewComponent
{
    private readonly NoPaynSettings _settings;

    public PaymentInfoViewComponent(NoPaynSettings settings)
    {
        _settings = settings;
    }

    public IViewComponentResult Invoke()
    {
        var model = new PaymentInfoModel();

        if (_settings.EnableCreditCard)
            model.AvailableMethods.Add(new PaymentMethodOption
            {
                Identifier = NoPaynDefaults.PaymentMethods.CreditCard,
                DisplayName = NoPaynDefaults.DisplayNames.CreditCard
            });

        if (_settings.EnableApplePay)
            model.AvailableMethods.Add(new PaymentMethodOption
            {
                Identifier = NoPaynDefaults.PaymentMethods.ApplePay,
                DisplayName = NoPaynDefaults.DisplayNames.ApplePay
            });

        if (_settings.EnableGooglePay)
            model.AvailableMethods.Add(new PaymentMethodOption
            {
                Identifier = NoPaynDefaults.PaymentMethods.GooglePay,
                DisplayName = NoPaynDefaults.DisplayNames.GooglePay
            });

        if (_settings.EnableVippsMobilePay)
            model.AvailableMethods.Add(new PaymentMethodOption
            {
                Identifier = NoPaynDefaults.PaymentMethods.VippsMobilePay,
                DisplayName = NoPaynDefaults.DisplayNames.VippsMobilePay
            });

        return View("~/Plugins/Payments.NoPayn/Views/PaymentInfo.cshtml", model);
    }
}
