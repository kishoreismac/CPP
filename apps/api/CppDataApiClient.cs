using System.Net.Http.Json;

namespace Cpp.Api;

public interface ICppDataApiClient
{
    Task<List<Product>> SearchProductsAsync(string query, string? criterion, CancellationToken ct);
    Task<List<Account>> SearchAccountsAsync(string query, int limit, CancellationToken ct);
    Task<Account?> GetAccountAsync(string accountId, CancellationToken ct);
    Task<Product?> GetProductAsync(string productId, CancellationToken ct);
    Task<List<DeliverToLocation>> GetDeliveryLocationsAsync(string shipToAccountId, CancellationToken ct);
    Task<Order?> GetOrderAsync(string orderId, CancellationToken ct);
    Task<Order> CreateOrderAsync(OrderRequest request, CancellationToken ct);
    Task<SubmissionResult> SubmitOrderAsync(string orderId, CancellationToken ct);
}

public sealed class CppDataApiClient(HttpClient httpClient, IConfiguration config) : ICppDataApiClient
{
    public Task<List<Product>> SearchProductsAsync(string query, string? criterion, CancellationToken ct)
    {
        var routeKey = NormalizeCriterion(criterion) switch
        {
            "productName" => "ProductByName",
            "activeIngredient" => "ProductByActiveIngredient",
            "itemNumber" => "ProductByItemNumber",
            _ => "ProductSearch"
        };
        var fallback = routeKey switch
        {
            "ProductByName" => "products/by-name?q={query}",
            "ProductByActiveIngredient" => "products/by-active-ingredient?q={query}",
            "ProductByItemNumber" => "products/by-item-number?q={query}",
            _ => "products/search?criterion=all&q={query}"
        };
        return GetListAsync<Product>(Route(routeKey, fallback, ("query", query)), ct);
    }

    public Task<List<Account>> SearchAccountsAsync(string query, int limit, CancellationToken ct) =>
      GetListAsync<Account>(Route("AccountSearch", "accounts/search?q={query}&limit={limit}", ("query", query), ("limit", limit.ToString())), ct);

    public async Task<Account?> GetAccountAsync(string accountId, CancellationToken ct) =>
      await httpClient.GetFromJsonAsync<Account>(Route("AccountById", "accounts/{accountId}", ("accountId", accountId)), ct);

    public async Task<Product?> GetProductAsync(string productId, CancellationToken ct) =>
      await httpClient.GetFromJsonAsync<Product>(Route("ProductById", "products/{productId}", ("productId", productId)), ct);

    public Task<List<DeliverToLocation>> GetDeliveryLocationsAsync(string shipToAccountId, CancellationToken ct) =>
      GetListAsync<DeliverToLocation>(Route("DeliveryLocations", "accounts/{accountId}/deliver-to-locations", ("accountId", shipToAccountId)), ct);

    public async Task<Order?> GetOrderAsync(string orderId, CancellationToken ct) =>
      await httpClient.GetFromJsonAsync<Order>(Route("OrderById", "orders/{orderId}", ("orderId", orderId)), ct);

    public async Task<Order> CreateOrderAsync(OrderRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync(Route("CreateOrder", "orders"), request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>(cancellationToken: ct)
          ?? throw new InvalidOperationException("The order API returned an empty response.");
    }

    public async Task<SubmissionResult> SubmitOrderAsync(string orderId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route("SubmitOrder", "orders/{orderId}/submit", ("orderId", orderId)));
        request.Headers.Add("Idempotency-Key", $"assistant-{orderId}");
        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmissionResult>(cancellationToken: ct)
          ?? throw new InvalidOperationException("The order submission API returned an empty response.");
    }

    async Task<List<T>> GetListAsync<T>(string route, CancellationToken ct) =>
      await httpClient.GetFromJsonAsync<List<T>>(route, ct) ?? [];

    string Route(string key, string fallback, params (string Name, string Value)[] values)
    {
        var route = config[$"DataApi:Routes:{key}"] ?? fallback;
        foreach (var (name, value) in values)
        {
            route = route.Replace($"{{{name}}}", Uri.EscapeDataString(value), StringComparison.OrdinalIgnoreCase);
        }
        return route;
    }

    static string NormalizeCriterion(string? value) => value?.Replace(" ", "").Replace("-", "").ToLowerInvariant() switch
    {
        "productname" or "name" => "productName",
        "activeingredient" or "ingredient" => "activeIngredient",
        "itemnumber" or "item" or "sku" => "itemNumber",
        _ => "all"
    };
}
