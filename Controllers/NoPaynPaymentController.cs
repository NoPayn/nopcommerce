using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Data;
using Nop.Plugin.Payments.NoPayn.Services;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Payments.NoPayn.Controllers;

public class NoPaynPaymentController : Controller
{
    private readonly NoPaynApiClient _apiClient;
    private readonly NoPaynLogger _nopaynLogger;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly IRepository<Order> _orderRepository;
    private readonly ILogger _logger;

    public NoPaynPaymentController(
        NoPaynApiClient apiClient,
        NoPaynLogger nopaynLogger,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        IRepository<Order> orderRepository,
        ILogger logger)
    {
        _apiClient = apiClient;
        _nopaynLogger = nopaynLogger;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    [HttpGet("NoPayn/PaymentReturn")]
    public async Task<IActionResult> PaymentReturn(int orderId)
    {
        _nopaynLogger.LogInfo($"PaymentReturn called for order #{orderId}");

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return RedirectToRoute("Homepage");

        var nopaynOrderId = order.AuthorizationTransactionId;
        if (string.IsNullOrEmpty(nopaynOrderId))
            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });

        try
        {
            var apiOrder = await _apiClient.GetOrderAsync(nopaynOrderId);
            var status = apiOrder?["status"]?.GetValue<string>() ?? "error";

            _nopaynLogger.LogInfo($"PaymentReturn: NoPayn order {nopaynOrderId} status={status}");

            // Preserve manual_capture flag if present
            var previousResult = order.AuthorizationTransactionResult ?? string.Empty;
            var manualCaptureFlag = previousResult.Contains("manual_capture") ? "|manual_capture" : string.Empty;
            order.AuthorizationTransactionResult = status + manualCaptureFlag;
            await _orderService.UpdateOrderAsync(order);

            if (status == "completed")
            {
                await MarkOrderAsPaidProcessingAsync(order);
            }
            else if (status is "cancelled" or "expired" or "error")
            {
                if (_orderProcessingService.CanCancelOrder(order))
                    await _orderProcessingService.CancelOrderAsync(order, true);

                return RedirectToRoute("OrderDetails", new { orderId = order.Id });
            }
        }
        catch (Exception ex)
        {
            _nopaynLogger.LogError($"PaymentReturn error for order #{orderId}", ex);
            await _logger.ErrorAsync("NoPayn PaymentReturn error", ex);
        }

        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
    }

    [HttpGet("NoPayn/PaymentCancel")]
    public async Task<IActionResult> PaymentCancel(int orderId)
    {
        _nopaynLogger.LogInfo($"PaymentCancel called for order #{orderId}");

        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return RedirectToRoute("Homepage");

        try
        {
            order.AuthorizationTransactionResult = "cancelled";
            await _orderService.UpdateOrderAsync(order);

            if (_orderProcessingService.CanCancelOrder(order))
                await _orderProcessingService.CancelOrderAsync(order, true);
        }
        catch (Exception ex)
        {
            _nopaynLogger.LogError($"PaymentCancel error for order #{orderId}", ex);
            await _logger.ErrorAsync("NoPayn PaymentCancel error", ex);
        }

        return RedirectToRoute("ShoppingCart");
    }

    [HttpPost("NoPayn/Webhook")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        _nopaynLogger.LogWebhook("Webhook received", body);

        JsonNode? data;
        try
        {
            data = JsonNode.Parse(body);
        }
        catch
        {
            _nopaynLogger.LogError("Webhook: failed to parse JSON body");
            return BadRequest();
        }

        var nopaynOrderId = data?["order_id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(nopaynOrderId))
        {
            _nopaynLogger.LogError("Webhook: missing order_id in payload");
            return BadRequest();
        }

        try
        {
            var order = _orderRepository.Table
                .FirstOrDefault(o => o.AuthorizationTransactionId == nopaynOrderId);

            if (order == null)
            {
                _nopaynLogger.LogInfo($"Webhook: no matching nopCommerce order found for NoPayn order {nopaynOrderId}");
                return Ok();
            }

            if (order.PaymentStatus is PaymentStatus.Paid
                or PaymentStatus.Voided
                or PaymentStatus.Refunded)
            {
                _nopaynLogger.LogInfo($"Webhook: order #{order.Id} already in final payment status {order.PaymentStatus}, skipping");
                return Ok();
            }

            var apiOrder = await _apiClient.GetOrderAsync(nopaynOrderId);
            var status = apiOrder?["status"]?.GetValue<string>() ?? "error";

            _nopaynLogger.LogInfo($"Webhook: NoPayn order {nopaynOrderId} status={status} for nopCommerce order #{order.Id}");

            // Preserve manual_capture flag if present
            var previousResult = order.AuthorizationTransactionResult ?? string.Empty;
            var manualCaptureFlag = previousResult.Contains("manual_capture") ? "|manual_capture" : string.Empty;
            order.AuthorizationTransactionResult = status + manualCaptureFlag;
            await _orderService.UpdateOrderAsync(order);

            if (status == "completed")
            {
                await MarkOrderAsPaidProcessingAsync(order);
            }
            else if (status is "cancelled" or "expired" or "error")
            {
                if (_orderProcessingService.CanCancelOrder(order))
                    await _orderProcessingService.CancelOrderAsync(order, true);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _nopaynLogger.LogError($"Webhook error for NoPayn order {nopaynOrderId}", ex);
            await _logger.ErrorAsync($"NoPayn webhook error for order {nopaynOrderId}", ex);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Sets payment to Paid and order to Processing.
    /// Deliberately avoids MarkOrderAsPaidAsync which auto-promotes to Complete.
    /// </summary>
    private async Task MarkOrderAsPaidProcessingAsync(Order order)
    {
        if (order.PaymentStatus == PaymentStatus.Paid)
            return;

        order.PaymentStatusId = (int)PaymentStatus.Paid;
        order.PaidDateUtc = DateTime.UtcNow;
        order.OrderStatusId = (int)OrderStatus.Processing;
        await _orderService.UpdateOrderAsync(order);

        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = "Payment confirmed by NoPayn. Order set to Processing.",
            DisplayToCustomer = false,
            CreatedOnUtc = DateTime.UtcNow
        });
    }
}
