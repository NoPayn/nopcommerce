using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.NoPayn.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.NoPayn.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NoPaynController : BasePaymentController
{
    private readonly NoPaynSettings _settings;
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;

    public NoPaynController(
        NoPaynSettings settings,
        ISettingService settingService,
        ILocalizationService localizationService,
        INotificationService notificationService)
    {
        _settings = settings;
        _settingService = settingService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            ApiKey = _settings.ApiKey,
            EnableCreditCard = _settings.EnableCreditCard,
            EnableApplePay = _settings.EnableApplePay,
            EnableGooglePay = _settings.EnableGooglePay,
            EnableVippsMobilePay = _settings.EnableVippsMobilePay,
            AdditionalFee = _settings.AdditionalFee,
            AdditionalFeePercentage = _settings.AdditionalFeePercentage
        };

        return View("~/Plugins/Payments.NoPayn/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return Configure();

        _settings.ApiKey = model.ApiKey;
        _settings.EnableCreditCard = model.EnableCreditCard;
        _settings.EnableApplePay = model.EnableApplePay;
        _settings.EnableGooglePay = model.EnableGooglePay;
        _settings.EnableVippsMobilePay = model.EnableVippsMobilePay;
        _settings.AdditionalFee = model.AdditionalFee;
        _settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

        await _settingService.SaveSettingAsync(_settings);
        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return Configure();
    }
}
