namespace Nop.Plugin.Payments.NoPayn.Models;

public class PaymentInfoModel
{
    public List<PaymentMethodOption> AvailableMethods { get; set; } = new();
}

public class PaymentMethodOption
{
    public string Identifier { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
