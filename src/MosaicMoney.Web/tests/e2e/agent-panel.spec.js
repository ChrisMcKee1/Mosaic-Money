import { expect, test } from "@playwright/test";
import { resetMockApi } from "./support/mockApi";

test.beforeEach(async ({ request }) => {
  await resetMockApi(request);
});

test("streams agent response in global panel", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Open agent" }).click();
  await expect(page.getByRole("heading", { name: "Policy-aware runtime agent" })).toBeVisible();

  await page.getByLabel("Ask agent").fill("Summarize my latest transaction activity.");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(
    page.getByText("Mock agent response: I reviewed your request and captured a deterministic summary."),
  ).toBeVisible();

  await page.getByRole("button", { name: "Provenance" }).click();
  await expect(page.getByText("Foundry agent invocation completed.")).toBeVisible();
});

test("queues approval-required actions and supports approval", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Open agent" }).click();
  await page.getByLabel("Ask agent").fill("Send this budget update to my partner.");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("High-impact action requires approval")).toBeVisible();
  await expect(page.getByText("1 pending")).toBeVisible();

  page.on("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Approve" }).click();

  await expect(page.getByText("approved")).toBeVisible();
});

