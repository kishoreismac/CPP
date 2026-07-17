# API contracts

Runtime OpenAPI: `/openapi/v1.json`.

Implemented resources include current user, accounts, dashboard messages/knowledge, orders CRUD/duplicate/validate/submit/export/PDF, product search/suggestions/details/favorites, and assistant messages. Standard failed submissions use Problem Details and every response includes `X-Correlation-ID`. Submission accepts `Idempotency-Key`; repeat calls to an already submitted order return the original confirmation identifiers.

Product search query parameters: `criterion`, `q`, and `favorites`. Criteria are Product Name, Item Number, Active Ingredient, Product Category, GTIN, Package Size, Vendor/Supplier, and Product Line.

Order writes contain account IDs, customer PO, contact/shipping fields, freight, requested arrival date, and exact SKU lines. Validation returns `severity`, `code`, `message`, `field`, and `suggestedResolution`.
