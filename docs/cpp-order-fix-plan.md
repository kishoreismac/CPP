# CPP Order Workflow Fix Plan

1. Correct the application shell so Orders contains only generic order navigation and Crop Protection owns an accessible nested CPP menu with route-aware active state.
2. Expand the existing account contract and deterministic seed data with four entitled Ship-To accounts and associated Deliver-To locations.
3. Replace plain account selects with reusable searchable comboboxes, populate account defaults, clear invalid delivery state on account changes, and implement validated alternate delivery details.
4. Centralize ranked product search/suggestions in the API, update the deterministic catalog, and repair the product drawer with debounced keyboard-accessible autocomplete and inventory-aware quantities.
5. Preserve all repaired fields and exact products through save, resume, review, and submission; add focused unit and Playwright coverage.
6. Run frontend format/lint/typecheck/unit/build, backend build/tests, runtime API checks, and Playwright end-to-end scenarios.
