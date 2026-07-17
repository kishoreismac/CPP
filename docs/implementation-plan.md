# CPP Order Management Portal — Implementation Plan

## Delivery approach

1. Establish a modular ASP.NET Core API, SQLite persistence, deterministic seed data, OpenAPI, and explicit adapter boundaries.
2. Build the React/TypeScript application shell, command center, orders workspace, and permission-aware navigation.
3. Implement the order lifecycle end to end: account defaults, catalog search, product selection, drafts, validation, review, fulfillment submission, confirmation, and PDF.
4. Connect the deterministic conversational assistant to the same order and catalog services.
5. Add unit, integration, and Playwright coverage; Docker/Compose and CI; then run all available quality gates and document results.

## Architecture decisions

- Modular monolith: React SPA plus one ASP.NET Core API.
- SQLite/EF Core is the local system of record; integration boundaries are expressed as C# interfaces with deterministic mock implementations.
- The API owns authorization, pricing, validation, state transitions, idempotent submission, audit events, and assistant draft mutations.
- The web client uses shared design tokens and feature modules; it never depends on a specific mock implementation.
- Demonstration data is synthetic and repeatable.

## Verification

- Frontend: formatting, ESLint, strict TypeScript, Vitest/RTL, production build.
- Backend: formatting verification, build, xUnit and API integration tests.
- End to end: Playwright against locally started API and web app.
- Packaging: Docker image builds and Compose configuration validation.
