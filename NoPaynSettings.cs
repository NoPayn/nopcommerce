using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.NoPayn;

public class NoPaynSettings : ISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public bool EnableCreditCard { get; set; } = true;
    public bool EnableApplePay { get; set; } = true;
    public bool EnableGooglePay { get; set; } = true;
    public bool EnableVippsMobilePay { get; set; } = true;
    public bool CreditCardManualCapture { get; set; }
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFeePercentage { get; set; }
    public bool DebugLogging { get; set; }
}
