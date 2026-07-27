using System.Net.Http.Headers;
using System.Text.Json;

namespace Cpp.Api;

public interface IProductCatalogClient
{
    Task<IReadOnlyList<Product>> SearchAsync(string category, string query, CancellationToken ct);
}

public sealed class ProductCatalogClient(HttpClient httpClient, IConfiguration configuration) : IProductCatalogClient
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<Product>> SearchAsync(string category, string query, CancellationToken ct)
    {
        var path = category switch
        {
            "itemNumber" => "api/products/search-by-item-number",
            "activeIngredient" => "api/products/search-by-active-ingredient",
            "productName" => "api/products/search-by-name",
            _ => throw new InvalidOperationException($"Unsupported product search category '{category}'.")
        };
        var requestUri = $"{path}?query={Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        var apiKey = configuration["ProductApi:ApiKey"];
        var apiKeyHeader = configuration["ProductApi:ApiKeyHeader"] ?? "X-API-Key";
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.TryAddWithoutValidation(apiKeyHeader, apiKey);
        var bearerToken = configuration["ProductApi:BearerToken"];
        if (!string.IsNullOrWhiteSpace(bearerToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        var productsNode = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("products", out var products) ? products
            : root.TryGetProperty("results", out var results) ? results
            : throw new InvalidOperationException("Product API response must be an array or contain a products/results array.");
        return JsonSerializer.Deserialize<List<Product>>(productsNode.GetRawText(), JsonOptions) ?? [];
    }
}
