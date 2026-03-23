using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.NoPayn.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.ApiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.EnableCreditCard")]
    public bool EnableCreditCard { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.EnableApplePay")]
    public bool EnableApplePay { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.EnableGooglePay")]
    public bool EnableGooglePay { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.EnableVippsMobilePay")]
    public bool EnableVippsMobilePay { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.CreditCardManualCapture")]
    public bool CreditCardManualCapture { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NoPayn.Fields.DebugLogging")]
    public bool DebugLogging { get; set; }
}
