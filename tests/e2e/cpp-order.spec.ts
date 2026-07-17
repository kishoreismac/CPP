import { test, expect } from "@playwright/test";
test("Crop Protection navigation owns CPP links", async ({ page }) => {
  await page.goto("/");
  await page.getByTestId("crop-protection-menu").hover();
  await expect(page.getByTestId("cpp-order-link")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Orders", exact: true }),
  ).toBeVisible();
  await page.getByTestId("cpp-order-link").click();
  await expect(
    page.getByRole("heading", { name: "Crop Protection Orders" }),
  ).toBeVisible();
});
test("Ship-To defaults, Deliver-To and product addition work", async ({
  page,
}) => {
  await page.goto("/crop-protection/orders/new");
  await expect(page.getByTestId("add-product")).toBeDisabled();
  await page.getByTestId("ship-to").click();
  await page.getByTestId("ship-to").fill("Adr");
  await page.getByRole("option", { name: /MFA-ADRIAN/ }).click();
  await expect(page.locator('input[value="WFU-MEMBER-1001"]')).toBeVisible();
  await expect(page.locator('input[value="orders.adrian@example.com"]')).toBeVisible();
  await expect(page.getByTestId("deliver-to")).toBeEnabled();
  await expect(page.getByTestId("add-product")).toBeEnabled();
  await page.getByTestId("add-product").click();
  await page.getByTestId("product-search").fill("st");
  const exactSkuSuggestion=page.getByRole("option", { name: /WU STERLING BLUE DGA 2.5G/ });
  await expect(exactSkuSuggestion).toBeVisible();
  await exactSkuSuggestion.click();
  await expect(
    page.getByText("WU STERLING BLUE DGA 2.5G", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Select WU STERLING BLUE DGA 2.5G").check();
  await page.getByLabel("WU STERLING BLUE DGA 2.5G quantity").fill("2");
  await page.getByTestId("confirm-products").click();
  await expect(page.getByText("Products added to your order.")).toBeVisible();
  await expect(
    page.getByText("WU STERLING BLUE DGA 2.5G", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Save Draft" }).click();
  await expect(page).toHaveURL(/\/crop-protection\/orders\/.+\/edit/);
  await expect(page.getByText(/Draft .+ saved\./)).toBeVisible();
});
test("Ship-To change replaces Deliver-To choices", async ({ page }) => {
  await page.goto("/crop-protection/orders/new");
  const ship = page.getByTestId("ship-to");
  await ship.click();
  await ship.fill("Boon");
  await page.getByRole("option", { name: /MFA-BOONVILLE/ }).click();
  await expect(page.getByTestId("deliver-to").locator("option")).toHaveCount(3);
  await ship.click();
  await ship.fill("Gall");
  await page.getByRole("option", { name: /MFA-GALLATIN/ }).click();
  await expect(page.getByTestId("deliver-to").locator("option")).toHaveCount(3);
  await expect(
    page.getByTestId("deliver-to").locator("option", { hasText: "Boonville" }),
  ).toHaveCount(0);
});
