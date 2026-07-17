# Architecture

The portal is a two-process modular monolith: a React SPA and one ASP.NET Core API backed by SQLite. API controllers expose DTO contracts; application services own deterministic validation and fulfillment; EF Core owns persistence. The frontend consumes HTTP contracts through one client module and is unaware whether adapters are mock or production implementations.

## Core flow

Account selection populates defaults → catalog search returns exact persisted SKUs → lines are merged into one structured draft → API persists draft and audit events → rule engine returns errors/warnings/information → explicit confirmation calls the fulfillment gateway → successful adapter response advances the state and creates confirmation data. Failures retain the order in `SubmissionFailed` for recovery.

## Replacement points

- Identity provider/current user
- Account master and entitlements
- Product catalog, inventory, and account pricing
- Business rules/structured delivery matrix
- Order orchestration/JDE fulfillment (`IFulfillmentGateway`)
- PDF generation
- Support handoff and conversational AI

Production implementations must add enterprise authentication/authorization, secret management, durable idempotency records, migrations, telemetry, and approved contracts.
