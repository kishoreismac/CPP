# CPP Order Management Portal

Enterprise-style B2B Crop Protection Products ordering proof of concept. The React SPA and ASP.NET Core modular API support command-center navigation, account-driven order entry, a 40-item catalog, ranked multi-field search, drafts, deterministic rule validation, review, mock JDE submission, confirmation PDF, CSV export, and a deterministic conversational assistant.

## Architecture

- `apps/web`: React 19, strict TypeScript, Vite, React Router, TanStack Query, shared CSS design tokens.
- `apps/api`: ASP.NET Core 10 Web API, EF Core, SQLite, OpenAPI, rule and fulfillment adapters.
- `docs`: implementation plan, architecture, API contracts, and client demo script.
- `tests/e2e`: Playwright scenarios.

The API is the authority for permissions, validation, order state, audit, and submission. `IFulfillmentGateway` and `IOrderRuleService` isolate replaceable external behavior.

## Prerequisites and local setup

- Node.js 22+
- .NET SDK 10+

```powershell
cd apps/web
npm install
npm run dev
```

In a second terminal:

```powershell
dotnet run --project apps/api --urls http://localhost:5090
```

Open `http://localhost:5173`. OpenAPI is available at `http://localhost:5090/openapi/v1.json` in Development.

Docker Compose: `docker compose up --build`.

## Quality commands

```powershell
cd apps/web
npm run format
npm run lint
npm run test
npm run build

cd ../..
dotnet format apps/api/Cpp.Api.csproj --verify-no-changes
dotnet build apps/api/Cpp.Api.csproj
dotnet test
```

Playwright: install browsers with `npx playwright install`, then run `npx playwright test` from `tests/e2e`.

## Main routes

- `/command-center`
- `/crop-protection/orders`
- `/crop-protection/orders/new`
- `/crop-protection/orders/:orderId/edit`
- `/crop-protection/orders/:orderId/review`
- `/crop-protection/orders/:orderId/confirmation`

## Demonstration data and controls

Seed data contains MFA-ADRIAN, MFA-BOONVILLE, MFA-GALLATIN, submitted and draft orders, and 40 products spanning the requested taxonomy. Searches for `ster`, `sterling blue`, `glyphosate`, item numbers, package sizes, and suppliers are supported. The user menu exposes a clearly labeled development role switcher; it is currently a UI demonstration control. Set `MockJde__FailSubmissions=true` to demonstrate safe downstream failure handling.

## Integration boundaries and POC limits

`MockJdeFulfillmentGateway` replaces a future JDE/MuleSoft adapter. The deterministic rule service, local catalog, inventory fields, prices, identity, PDF writer, support flow, and assistant parser are replaceable local implementations. Real identity, Evolve, account master, catalog, pricing, inventory, structured delivery matrix, JDE, and conversational-AI systems are **not connected** and require approved API contracts and business-rule confirmation.

All products, agronomic attributes, availability, addresses, prices, and inventory are synthetic demonstration data. They are not authoritative, current, or suitable for agronomic, regulatory, purchasing, or fulfillment decisions.
