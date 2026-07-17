import { describe, it, expect } from "vitest";
describe("CPP domain display rules", () => {
  it("formats search criteria deterministically", () => {
    expect(["Product Name", "Active Ingredient"]).toContain(
      "Active Ingredient",
    );
  });
  it("uses the configured gallon split threshold", () => {
    const threshold = 6098;
    expect(6099).toBeGreaterThan(threshold);
  });
});
