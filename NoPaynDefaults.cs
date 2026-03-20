namespace Nop.Plugin.Payments.NoPayn;

public static class NoPaynDefaults
{
    public const string SystemName = "Payments.NoPayn";
    public const string ApiBaseUrl = "https://api.nopayn.co.uk";
    public const string ExpirationPeriod = "PT5M";

    public static class PaymentMethods
    {
        public const string CreditCard = "credit-card";
        public const string ApplePay = "apple-pay";
        public const string GooglePay = "google-pay";
        public const string VippsMobilePay = "vipps-mobilepay";
    }

    public static class DisplayNames
    {
        public const string CreditCard = "Credit / Debit Card";
        public const string ApplePay = "Apple Pay";
        public const string GooglePay = "Google Pay";
        public const string VippsMobilePay = "Vipps MobilePay";
    }

    public static class AdminDisplayNames
    {
        public const string CreditCard = "NoPayn Credit / Debit Card";
        public const string ApplePay = "NoPayn Apple Pay";
        public const string GooglePay = "NoPayn Google Pay";
        public const string VippsMobilePay = "NoPayn Vipps MobilePay";
    }
}
