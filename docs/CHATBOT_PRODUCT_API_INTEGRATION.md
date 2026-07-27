# Chatbot product API integration

The chatbot classifies product requests into `productName`, `itemNumber`, or `activeIngredient`. It does not ask the model to execute a product-search tool. The ASP.NET Core backend maps the classification to an HTTP endpoint exposed by the external product application.

| Category | HTTP endpoint |
| --- | --- |
| `productName` | `GET api/products/search-by-name?query=...` |
| `itemNumber` | `GET api/products/search-by-item-number?query=...` |
| `activeIngredient` | `GET api/products/search-by-active-ingredient?query=...` |

Configure the external application in environment-specific configuration or environment variables:

```json
{
  "ProductApi": {
    "BaseUrl": "https://product-api.example.com/",
    "TimeoutSeconds": 30,
    "ApiKeyHeader": "X-API-Key",
    "ApiKey": "use-a-secret-provider",
    "BearerToken": "use-a-secret-provider"
  }
}
```

Use either an API key or bearer token, according to the external team's contract. Do not commit credentials. The equivalent environment variables use double underscores, for example `ProductApi__BaseUrl` and `ProductApi__ApiKey`.

Each endpoint may return either a JSON product array or an object containing a `products` or `results` array. Product fields must be compatible with the API's `Product` contract. The backend sends returned records to Azure OpenAI only for grounded summarization and returns the records separately for structured UI cards.

For local development, `appsettings.Development.json` points `ProductApi:BaseUrl` to the locally running application API. Replace it with the external team's development URL when available.
