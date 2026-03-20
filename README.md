# NoPayn Payment Plugin for nopCommerce

Accept Credit/Debit Cards, Apple Pay, Google Pay, and Vipps MobilePay in your nopCommerce store via NoPayn.

## Requirements

- nopCommerce 4.90+
- .NET 9
- NoPayn merchant account ([manage.nopayn.io](https://manage.nopayn.io/))

## Installation

1. Copy the plugin folder to `src/Plugins/Nop.Plugin.Payments.NoPayn/` in your nopCommerce source
2. Add the project to your solution: `dotnet sln add src/Plugins/Nop.Plugin.Payments.NoPayn/Nop.Plugin.Payments.NoPayn.csproj`
3. Build the solution
4. In admin panel, go to **Configuration > Local plugins** and install "NoPayn Payment Gateway"
5. Go to **Configuration > Payment methods** and activate it
6. Click **Configure** to enter your API key and enable/disable payment methods

## Configuration

| Setting | Description |
|---------|-------------|
| API Key | Your NoPayn API key from [manage.nopayn.io](https://manage.nopayn.io/) |
| Enable Credit / Debit Card | Toggle credit/debit card payments |
| Enable Apple Pay | Toggle Apple Pay payments |
| Enable Google Pay | Toggle Google Pay payments |
| Enable Vipps MobilePay | Toggle Vipps MobilePay payments |

## Payment Methods

| Checkout Display Name | Backend Display Name | NoPayn Identifier |
|-----------------------|----------------------|-------------------|
| Credit / Debit Card | NoPayn Credit / Debit Card | `credit-card` |
| Apple Pay | NoPayn Apple Pay | `apple-pay` |
| Google Pay | NoPayn Google Pay | `google-pay` |
| Vipps MobilePay | NoPayn Vipps MobilePay | `vipps-mobilepay` |

## Payment Flow

1. Customer selects "NoPayn Payment Gateway" at checkout
2. Sub-method selection appears (Credit Card, Apple Pay, etc.)
3. Customer picks a method and confirms the order
4. Order is created with **Pending** payment status
5. Plugin calls `POST /v1/orders/` with `transactions[].payment_method`
6. Customer is redirected to the NoPayn payment page (direct method, not HPP)
7. On success → order marked as **Paid / Processing**
8. On cancel/failure/expiry → order **Cancelled**
9. Webhook confirms final status asynchronously

## Order Status Mapping

| NoPayn Status | nopCommerce Payment Status | nopCommerce Order Status |
|---------------|---------------------------|--------------------------|
| new | Pending | Pending |
| processing | Pending | Pending |
| completed | Paid | Processing |
| cancelled | Voided | Cancelled |
| expired | Voided | Cancelled |
| error | Voided | Cancelled |

## Webhook

The plugin registers a webhook endpoint at `/NoPayn/Webhook`. NoPayn sends a POST with `{"order_id": "..."}` on status changes. The plugin verifies the status via `GET /v1/orders/{id}/` before updating the order.

## Technical Details

- **Expiration**: 5 minutes (`PT5M`) per transaction
- **API**: Uses `transactions` array with `payment_method` (direct redirect, not HPP filter)
- **Authentication**: HTTP Basic (API key as username, empty password)
- **Transaction data**: Stored in nopCommerce order fields (`AuthorizationTransactionId`, `AuthorizationTransactionCode`, `AuthorizationTransactionResult`)

## License

Free — provided by [Cost+](https://costplus.io)

## Support

- NoPayn Developer Docs: [dev.nopayn.io](https://dev.nopayn.io/)
- Cost+ Support: [costplus.io](https://costplus.io)
