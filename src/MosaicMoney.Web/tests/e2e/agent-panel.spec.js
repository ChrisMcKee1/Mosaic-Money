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

  await page.getByRole("button", { name: "Runs" }).click();
  await expect(page.getByText("Foundry agent invocation completed.")).toBeVisible();
});

test("queues approval-required actions and supports approval", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Open agent" }).click();
  await page.getByLabel("Ask agent").fill("Send this budget update to my partner.");
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("High-impact action requires approval")).toBeVisible();
  await expect(page.getByTestId("agent-action-review-pending")).toHaveText("1 pending");

  page.on("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Approve" }).click();

  await expect(page.getByText("approved")).toBeVisible();
});

test("generates reusable prompt from current draft", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Open agent" }).click();
  await page.getByLabel("Ask agent").fill("track my budget drift and highlight overspend risks this week");

  await page.getByRole("button", { name: "Generate Reusable" }).click();

  const editorForm = page.locator("form").filter({ hasText: /Save reusable prompt|Edit saved prompt/i });
  const titleInput = editorForm.getByRole("textbox", { name: "Prompt title" });
  const promptInput = editorForm.getByRole("textbox", { name: "Reusable prompt", exact: true });

  await expect(titleInput).not.toHaveValue("");
  await expect(promptInput).toHaveValue("track my budget drift and highlight overspend risks this week");

  await editorForm.evaluate((form) => form.requestSubmit());
  await expect(page.getByText("track my budget drift and highlight", { exact: true })).toBeVisible();
});

test("generates AI title and polishes initial prompt in editor", async ({ page }) => {
  await page.goto("/");

  await page.getByRole("button", { name: "Open agent" }).click();
  await page.getByRole("button", { name: "Browse" }).click();
  await page.getByLabel("Ask agent").fill("summarize   recurring bills   and cashflow");

  await page.getByRole("button", { name: "Save Draft" }).click();

  const editorForm = page.locator("form").filter({ hasText: /Save reusable prompt|Edit saved prompt/i });
  const titleInput = editorForm.getByRole("textbox", { name: "Prompt title" });
  const promptInput = editorForm.getByRole("textbox", { name: "Reusable prompt", exact: true });
  const aiTitleButton = editorForm.getByRole("button", { name: "AI Title", exact: true });
  const aiPolishButton = editorForm.getByRole("button", { name: "AI Polish + Title", exact: true });

  await aiTitleButton.evaluate((button) => button.click());
  await expect(titleInput).not.toHaveValue("");

  await aiPolishButton.evaluate((button) => button.click());
  await expect(promptInput).toHaveValue("summarize recurring bills and cashflow");
});

