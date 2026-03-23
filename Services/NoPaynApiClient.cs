using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nop.Plugin.Payments.NoPayn.Services;

public class NoPaynApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NoPaynSettings _settings;
    private readonly NoPaynLogger _nopaynLogger;

    public NoPaynApiClient(IHttpClientFactory httpClientFactory, NoPaynSettings settings, NoPaynLogger nopaynLogger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _nopaynLogger = nopaynLogger;
    }

    public async Task<JsonNode?> CreateOrderAsync(object parameters)
    {
        return await SendAsync(HttpMethod.Post, "/v1/orders/", parameters);
    }

    public async Task<JsonNode?> GetOrderAsync(string orderId)
    {
        return await SendAsync(HttpMethod.Get, $"/v1/orders/{Uri.EscapeDataString(orderId)}/");
    }

    public async Task<JsonNode?> CreateRefundAsync(string orderId, int amountCents, string description = "")
    {
        return await SendAsync(HttpMethod.Post,
            $"/v1/orders/{Uri.EscapeDataString(orderId)}/refunds/",
            new { amount = amountCents, description });
    }

    public async Task<JsonNode?> CaptureTransactionAsync(string orderId, string transactionId)
    {
        return await SendAsync(HttpMethod.Post,
            $"/v1/orders/{Uri.EscapeDataString(orderId)}/transactions/{Uri.EscapeDataString(transactionId)}/captures/");
    }

    public async Task<JsonNode?> VoidTransactionAsync(string orderId, string transactionId, int amountInCents, string description = "")
    {
        return await SendAsync(HttpMethod.Post,
            $"/v1/orders/{Uri.EscapeDataString(orderId)}/transactions/{Uri.EscapeDataString(transactionId)}/voids/amount/",
            new { amount = amountInCents, description });
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string endpoint, object? body = null)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(NoPaynDefaults.ApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);

        var authBytes = Encoding.ASCII.GetBytes($"{_settings.ApiKey}:");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        if (method == HttpMethod.Get)
        {
            _nopaynLogger.LogApiRequest(method.Method, endpoint);
            response = await client.GetAsync(endpoint);
        }
        else
        {
            var json = JsonSerializer.Serialize(body);
            _nopaynLogger.LogApiRequest(method.Method, endpoint, json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await client.PostAsync(endpoint, content);
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        _nopaynLogger.LogApiResponse(method.Method, endpoint, (int)response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"NoPayn API error: HTTP {(int)response.StatusCode} — {responseBody}");

        return JsonNode.Parse(responseBody);
    }
}
