using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.Payments.NoPayn.Services;
using Nop.Services.Events;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.NoPayn.Infrastructure;

/// <summary>
/// Handles order status changes for NoPayn manual capture and void operations.
/// When manual capture is enabled for credit card, this consumer:
/// - Captures the transaction when order moves to Processing, Shipped, or Complete.
/// - Voids the transaction when order is Cancelled (if not yet captured).
/// </summary>
public class OrderStatusChangedEventConsumer : IConsumer<OrderStatusChangedEvent>
{
    private readonly NoPaynApiClient _apiClient;
    private readonly NoPaynLogger _nopaynLogger;
    private readonly IOrderService _orderService;
    private readonly IPaymentPluginManager _paymentPluginManager;

    public OrderStatusChangedEventConsumer(
        NoPaynApiClient apiClient,
        NoPaynLogger nopaynLogger,
        IOrderService orderService,
        IPaymentPluginManager paymentPluginManager)
    {
        _apiClient = apiClient;
        _nopaynLogger = nopaynLogger;
        _orderService = orderService;
        _paymentPluginManager = paymentPluginManager;
    }

    public async Task HandleEventAsync(OrderStatusChangedEvent eventMessage)
    {
        var order = eventMessage.Order;
        if (order == null)
            return;

        // Only handle NoPayn orders
        var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(NoPaynDefaults.SystemName);
        if (paymentMethod == null)
            return;

        if (order.PaymentMethodSystemName != NoPaynDefaults.SystemName)
            return;

        // Only handle orders with manual capture
        var transactionResult = order.AuthorizationTransactionResult ?? string.Empty;
        if (!transactionResult.Contains("manual_capture"))
            return;

        var nopaynOrderId = order.AuthorizationTransactionId;
        if (string.IsNullOrEmpty(nopaynOrderId))
            return;

        var newStatus = (OrderStatus)order.OrderStatusId;
        var previousStatus = eventMessage.PreviousOrderStatus;

        _nopaynLogger.LogInfo($"Order status changed for #{order.Id}: {previousStatus} -> {newStatus}, transaction result: {transactionResult}");

        // Capture on Processing, Shipped, or Complete (if not already captured)
        if (newStatus is OrderStatus.Processing or OrderStatus.Complete &&
            !transactionResult.Contains("captured"))
        {
            await HandleCaptureAsync(order, nopaynOrderId);
        }
        // Void on Cancel (if authorized but not captured)
        else if (newStatus == OrderStatus.Cancelled &&
                 !transactionResult.Contains("captured"))
        {
            await HandleVoidAsync(order, nopaynOrderId);
        }
    }

    private async Task HandleCaptureAsync(Order order, string nopaynOrderId)
    {
        try
        {
            _nopaynLogger.LogInfo($"Attempting capture for NoPayn order {nopaynOrderId} (nopCommerce order #{order.Id})");

            // Get the order to find the transaction ID
            var apiOrder = await _apiClient.GetOrderAsync(nopaynOrderId);
            var transactionId = apiOrder?["transactions"]?[0]?["id"]?.GetValue<string>();

            if (string.IsNullOrEmpty(transactionId))
            {
                _nopaynLogger.LogError($"Cannot capture: no transaction ID found for NoPayn order {nopaynOrderId}");
                await InsertOrderNoteAsync(order, "NoPayn capture failed: no transaction ID found on the NoPayn order.");
                return;
            }

            var result = await _apiClient.CaptureTransactionAsync(nopaynOrderId, transactionId);

            // Update transaction result to indicate capture
            order.AuthorizationTransactionResult = (order.AuthorizationTransactionResult ?? string.Empty)
                .Replace("manual_capture", "manual_capture|captured");
            await _orderService.UpdateOrderAsync(order);

            _nopaynLogger.LogInfo($"Capture successful for NoPayn order {nopaynOrderId}, transaction {transactionId}");
            await InsertOrderNoteAsync(order, $"NoPayn payment captured successfully. Transaction: {transactionId}");
        }
        catch (Exception ex)
        {
            _nopaynLogger.LogError($"Capture failed for NoPayn order {nopaynOrderId}", ex);
            await InsertOrderNoteAsync(order, $"NoPayn capture failed: {ex.Message}");
        }
    }

    private async Task HandleVoidAsync(Order order, string nopaynOrderId)
    {
        try
        {
            var amountCents = (int)Math.Round(order.OrderTotal * 100);
            _nopaynLogger.LogInfo($"Attempting void for NoPayn order {nopaynOrderId} (nopCommerce order #{order.Id}), amount: {amountCents}");

            // Get the order to find the transaction ID
            var apiOrder = await _apiClient.GetOrderAsync(nopaynOrderId);
            var transactionId = apiOrder?["transactions"]?[0]?["id"]?.GetValue<string>();

            if (string.IsNullOrEmpty(transactionId))
            {
                _nopaynLogger.LogError($"Cannot void: no transaction ID found for NoPayn order {nopaynOrderId}");
                await InsertOrderNoteAsync(order, "NoPayn void failed: no transaction ID found on the NoPayn order.");
                return;
            }

            var result = await _apiClient.VoidTransactionAsync(nopaynOrderId, transactionId, amountCents,
                $"Order #{order.CustomOrderNumber ?? order.Id.ToString()} cancelled");

            // Update transaction result to indicate void
            order.AuthorizationTransactionResult = (order.AuthorizationTransactionResult ?? string.Empty)
                .Replace("manual_capture", "manual_capture|voided");
            await _orderService.UpdateOrderAsync(order);

            _nopaynLogger.LogInfo($"Void successful for NoPayn order {nopaynOrderId}, transaction {transactionId}");
            await InsertOrderNoteAsync(order, $"NoPayn payment voided successfully. Transaction: {transactionId}");
        }
        catch (Exception ex)
        {
            _nopaynLogger.LogError($"Void failed for NoPayn order {nopaynOrderId}", ex);
            await InsertOrderNoteAsync(order, $"NoPayn void failed: {ex.Message}");
        }
    }

    private async Task InsertOrderNoteAsync(Order order, string note)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }
}
