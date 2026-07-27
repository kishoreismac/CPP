using Microsoft.EntityFrameworkCore;

namespace Cpp.Api;

public enum ProductSearchCategory { ProductName, ItemNumber, ActiveIngredient, ProductCategory, Gtin, PackageSize, Supplier, ProductLine }

public record ProductSearchResult(ProductSearchCategory Category, string Query, int TotalMatches, IReadOnlyList<Product> Products);

public interface IProductSearchService
{
    Task<ProductSearchResult> SearchAsync(ProductSearchCategory category, string? query, bool favorites, int? limit, CancellationToken ct);
    Task<ProductSearchResult> SearchByNameAsync(string? query, bool favorites, int? limit, CancellationToken ct);
    Task<ProductSearchResult> SearchByItemNumberAsync(string? query, bool favorites, int? limit, CancellationToken ct);
    Task<ProductSearchResult> SearchByActiveIngredientAsync(string? query, bool favorites, int? limit, CancellationToken ct);
}

public sealed class ProductSearchService(AppDb db) : IProductSearchService
{
    public Task<ProductSearchResult> SearchByNameAsync(string? query, bool favorites, int? limit, CancellationToken ct) => SearchAsync(ProductSearchCategory.ProductName, query, favorites, limit, ct);
    public Task<ProductSearchResult> SearchByItemNumberAsync(string? query, bool favorites, int? limit, CancellationToken ct) => SearchAsync(ProductSearchCategory.ItemNumber, query, favorites, limit, ct);
    public Task<ProductSearchResult> SearchByActiveIngredientAsync(string? query, bool favorites, int? limit, CancellationToken ct) => SearchAsync(ProductSearchCategory.ActiveIngredient, query, favorites, limit, ct);

    public async Task<ProductSearchResult> SearchAsync(ProductSearchCategory category, string? query, bool favorites, int? limit, CancellationToken ct)
    {
        var normalized = Normalize(query ?? string.Empty);
        var databaseQuery = db.Products.AsNoTracking().AsQueryable();
        if (favorites) databaseQuery = databaseQuery.Where(product => product.Favorite);
        var candidates = await databaseQuery.ToListAsync(ct);
        string Field(Product product) => category switch
        {
            ProductSearchCategory.ItemNumber => product.ItemNumber,
            ProductSearchCategory.ActiveIngredient => product.ActiveIngredients,
            ProductSearchCategory.ProductCategory => product.Category,
            ProductSearchCategory.Gtin => product.Gtin,
            ProductSearchCategory.PackageSize => product.PackageSize,
            ProductSearchCategory.Supplier => product.Supplier,
            ProductSearchCategory.ProductLine => product.ProductLine,
            _ => product.Name
        };
        var normalizedToken = SearchToken(normalized);
        var matches = candidates.Where(product => normalized.Length == 0
            || Field(product).Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || (normalizedToken.Length > 0 && SearchToken(Field(product)).Contains(normalizedToken, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(product => Rank(Field(product), normalized)).ThenBy(product => product.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var products = limit is > 0 ? matches.Take(limit.Value).ToList() : matches;
        return new(category, normalized, matches.Count, products);
    }

    public static ProductSearchCategory ParseCategory(string? value) => value?.Replace(" ", string.Empty).Replace("/", string.Empty).ToLowerInvariant() switch
    {
        "itemnumber" => ProductSearchCategory.ItemNumber, "activeingredient" => ProductSearchCategory.ActiveIngredient,
        "productcategory" => ProductSearchCategory.ProductCategory, "gtin" => ProductSearchCategory.Gtin,
        "packagesize" => ProductSearchCategory.PackageSize, "vendorsupplier" or "supplier" => ProductSearchCategory.Supplier,
        "productline" => ProductSearchCategory.ProductLine, _ => ProductSearchCategory.ProductName
    };
    public static string Normalize(string value) => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    static string SearchToken(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    public static int Rank(string value, string query) { if (value.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0; if (value.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1; if (value.Split([' ', ',', '-', '/'], StringSplitOptions.RemoveEmptyEntries).Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase))) return 2; return 3; }
    public static string MatchType(string value, string query) => Rank(value, query) switch { 0 => "exact", 1 => "startsWith", 2 => "wordStartsWith", _ => "contains" };
}
