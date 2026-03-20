using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NoPayn.Components;
using Nop.Plugin.Payments.NoPayn.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.NoPayn;

public class NoPaynPlugin : BasePlugin, IPaymentMethod
{
    private readonly NoPaynSettings _settings;
    private readonly NoPaynApiClient _apiClient;
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOrderService _orderService;

    public NoPaynPlugin(
        NoPaynSettings settings,
        NoPaynApiClient apiClient,
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper,
        IHttpContextAccessor httpContextAccessor,
        IOrderService orderService)
    {
        _settings = settings;
        _apiClient = apiClient;
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
        _httpContextAccessor = httpContextAccessor;
        _orderService = orderService;
    }

    public bool SupportCapture => false;
    public bool SupportPartiallyRefund => false;
    public bool SupportRefund => false;
    public bool SupportVoid => false;
    public bool SkipPaymentInfo => false;
    public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;
    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult
        {
            NewPaymentStatus = PaymentStatus.Pending
        });
    }

    public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        var order = postProcessPaymentRequest.Order;

        var nopaynMethod = GetCustomValue(order.CustomValuesXml, "NoPaynMethod")
                           ?? NoPaynDefaults.PaymentMethods.CreditCard;

        var storeUrl = _webHelper.GetStoreLocation();
        var amountCents = (int)Math.Round(order.OrderTotal * 100);
        var orderNumber = order.CustomOrderNumber ?? order.Id.ToString();

        var apiParams = new
        {
            currency = order.CustomerCurrencyCode,
            amount = amountCents,
            merchant_order_id = orderNumber,
            description = $"Order #{orderNumber}",
            return_url = $"{storeUrl}NoPayn/PaymentReturn?orderId={order.Id}",
            failure_url = $"{storeUrl}NoPayn/PaymentCancel?orderId={order.Id}",
            webhook_url = $"{storeUrl}NoPayn/Webhook",
            transactions = new[]
            {
                new
                {
                    payment_method = nopaynMethod,
                    expiration_period = NoPaynDefaults.ExpirationPeriod
                }
            }
        };

        var result = await _apiClient.CreateOrderAsync(apiParams);
        var nopaynOrderId = result?["id"]?.GetValue<string>();

        if (string.IsNullOrEmpty(nopaynOrderId))
            throw new NopException("Failed to create NoPayn order — no order ID returned.");

        order.AuthorizationTransactionId = nopaynOrderId;
        order.AuthorizationTransactionCode = nopaynMethod;
        order.AuthorizationTransactionResult = "new";
        await _orderService.UpdateOrderAsync(order);

        var paymentUrl = result?["transactions"]?[0]?["payment_url"]?.GetValue<string>()
                         ?? result?["order_url"]?.GetValue<string>();

        if (string.IsNullOrEmpty(paymentUrl))
            throw new NopException("No payment URL returned from NoPayn.");

        _httpContextAccessor.HttpContext?.Response.Redirect(paymentUrl);
    }

    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            return Task.FromResult(true);

        if (!_settings.EnableCreditCard && !_settings.EnableApplePay &&
            !_settings.EnableGooglePay && !_settings.EnableVippsMobilePay)
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(_settings.AdditionalFee);
    }

    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        => Task.FromResult(new CapturePaymentResult { Errors = ["Capture not supported"] });

    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        => Task.FromResult(new RefundPaymentResult { Errors = ["Refund through admin not supported yet"] });

    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        => Task.FromResult(new VoidPaymentResult { Errors = ["Void not supported"] });

    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        => Task.FromResult(new ProcessPaymentResult { Errors = ["Recurring payments not supported"] });

    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelRecurringPaymentRequest)
        => Task.FromResult(new CancelRecurringPaymentResult { Errors = ["Recurring payments not supported"] });

    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        if (order == null)
            return Task.FromResult(false);

        return Task.FromResult(
            order.PaymentStatus == PaymentStatus.Pending &&
            (DateTime.UtcNow - order.CreatedOnUtc).TotalMinutes < 5);
    }

    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        var errors = new List<string>();
        var selectedMethod = form["NoPaynMethod"].ToString();
        if (string.IsNullOrEmpty(selectedMethod))
            errors.Add("Please select a payment method.");
        return Task.FromResult<IList<string>>(errors);
    }

    public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        var request = new ProcessPaymentRequest();
        var selectedMethod = form["NoPaynMethod"].ToString();
        if (!string.IsNullOrEmpty(selectedMethod))
            request.CustomValues["NoPaynMethod"] = selectedMethod;
        return Task.FromResult(request);
    }

    public Type GetPublicViewComponent() => typeof(PaymentInfoViewComponent);

    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.NoPayn.PaymentMethodDescription");
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NoPayn/Configure";
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new NoPaynSettings
        {
            ApiKey = string.Empty,
            EnableCreditCard = true,
            EnableApplePay = true,
            EnableGooglePay = true,
            EnableVippsMobilePay = true
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NoPayn.Fields.ApiKey"] = "API Key",
            ["Plugins.Payments.NoPayn.Fields.ApiKey.Hint"] = "Enter your NoPayn API key from https://manage.nopayn.io/",
            ["Plugins.Payments.NoPayn.Fields.EnableCreditCard"] = "Enable Credit / Debit Card",
            ["Plugins.Payments.NoPayn.Fields.EnableApplePay"] = "Enable Apple Pay",
            ["Plugins.Payments.NoPayn.Fields.EnableGooglePay"] = "Enable Google Pay",
            ["Plugins.Payments.NoPayn.Fields.EnableVippsMobilePay"] = "Enable Vipps MobilePay",
            ["Plugins.Payments.NoPayn.Fields.AdditionalFee"] = "Additional Fee",
            ["Plugins.Payments.NoPayn.Fields.AdditionalFeePercentage"] = "Additional Fee (percentage)",
            ["Plugins.Payments.NoPayn.PaymentMethodDescription"] = "Pay with Credit/Debit Card, Apple Pay, Google Pay, or Vipps MobilePay",
            ["Plugins.Payments.NoPayn.Instructions"] = "Select your preferred payment method:",
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<NoPaynSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NoPayn");
        await base.UninstallAsync();
    }

    private static string? GetCustomValue(string? xml, string key)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        try
        {
            var doc = XDocument.Parse(xml);
            if (doc.Root == null) return null;

            // nopCommerce serializes as: <CustomValues><key>value</key></CustomValues>
            var directElement = doc.Root.Element(key);
            if (directElement != null)
                return directElement.Value;

            // Alternative format: <CustomValues><item><key>K</key><value>V</value></item></CustomValues>
            foreach (var item in doc.Root.Elements())
            {
                var k = item.Element("Key")?.Value ?? item.Element("key")?.Value;
                if (k == key)
                    return item.Element("Value")?.Value ?? item.Element("value")?.Value;
            }
        }
        catch { /* ignore parse errors */ }
        return null;
    }
}
