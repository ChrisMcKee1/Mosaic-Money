import { chromium } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import { loadLocalTriageEnv } from "./load-local-triage-env.mjs";

loadLocalTriageEnv();

const baseUrl = process.env.MM_TRIAGE_BASE_URL ?? "http://localhost:53832";
const outputDir = path.resolve("../../artifacts/release-gates/mm-qa-04/live-triage");

const partnerA = {
  email: process.env.MM_PARTNER_A_EMAIL,
  password: process.env.MM_PARTNER_A_PASSWORD,
};

const partnerB = {
  email: process.env.MM_PARTNER_B_EMAIL,
  password: process.env.MM_PARTNER_B_PASSWORD,
};

async function fillAll(locator, value) {
  const count = await locator.count();
  for (let index = 0; index < count; index += 1) {
    await locator.nth(index).fill(value);
  }
}

async function signIn(page, email, password) {
  await page.goto(`${baseUrl}/sign-in`, { waitUntil: "domcontentloaded" });

  const identifierInputs = page.locator(
    'input[name="identifier"]:visible, input[type="email"]:visible, input[autocomplete="username"]:visible, input[id*="email" i]:visible, input[type="text"]:visible',
  );
  const passwordInputs = page.locator('input[type="password"]:visible, input[name="password"]:visible');

  await identifierInputs.first().waitFor({ state: "visible", timeout: 25000 });
  await passwordInputs.first().waitFor({ state: "visible", timeout: 25000 });

  await fillAll(identifierInputs, email);
  await fillAll(passwordInputs, password);

  const submitButtons = page.locator(
    'button:has-text("Continue"):visible, button[type="submit"]:visible, button:has-text("Sign In"):visible, button:has-text("Sign in"):visible',
  );

  const submitCount = await submitButtons.count();
  if (!submitCount) {
    throw new Error("Unable to find sign-in submit button.");
  }

  await submitButtons.nth(submitCount - 1).click({ timeout: 5000 });
  await page.waitForTimeout(2500);
  await page.waitForLoadState("networkidle").catch(() => null);

  if (page.url().includes("/sign-in/factor-one")) {
    await fillAll(passwordInputs, password);

    const retryCount = await submitButtons.count();
    if (retryCount) {
      await submitButtons.nth(retryCount - 1).click({ timeout: 5000 });
      await page.waitForTimeout(2500);
      await page.waitForLoadState("networkidle").catch(() => null);
    }
  }
}

async function run() {
  if (!partnerA.email || !partnerA.password || !partnerB.email || !partnerB.password) {
    throw new Error("Missing MM_PARTNER_A_* or MM_PARTNER_B_* environment variables.");
  }

  await fs.mkdir(outputDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  const summary = {
    baseUrl,
    timestampUtc: new Date().toISOString(),
    partnerAAfterSignInUrl: null,
    signOutFinalUrl: null,
    partnerBAfterSignInUrl: null,
    success: false,
    screenshots: {
      partnerASignedIn: "live-triage/signout-roundtrip-partner-a.png",
      afterSignOut: "live-triage/signout-roundtrip-signed-out.png",
      partnerBSignedIn: "live-triage/signout-roundtrip-partner-b.png",
    },
    error: null,
  };

  try {
    await signIn(page, partnerA.email, partnerA.password);
    summary.partnerAAfterSignInUrl = page.url();
    await page.screenshot({ path: path.join(outputDir, "signout-roundtrip-partner-a.png"), fullPage: true });

    const signOutButton = page.getByRole("button", { name: "Sign Out" });
    await signOutButton.waitFor({ state: "visible", timeout: 15000 });
    await signOutButton.click({ timeout: 5000 });
    await page.waitForURL(/\/sign-in|\/$/, { timeout: 30000 });
    await page.waitForLoadState("networkidle").catch(() => null);

    summary.signOutFinalUrl = page.url();
    await page.screenshot({ path: path.join(outputDir, "signout-roundtrip-signed-out.png"), fullPage: true });

    await signIn(page, partnerB.email, partnerB.password);
    summary.partnerBAfterSignInUrl = page.url();
    await page.screenshot({ path: path.join(outputDir, "signout-roundtrip-partner-b.png"), fullPage: true });

    summary.success = !summary.partnerBAfterSignInUrl.includes("/sign-in");
  } catch (error) {
    summary.error = String(error);
  } finally {
    await context.close();
    await browser.close();
  }

  const summaryPath = path.join(outputDir, "signout-roundtrip-summary.json");
  await fs.writeFile(summaryPath, JSON.stringify(summary, null, 2), "utf8");

  if (!summary.success) {
    throw new Error(`Sign-out roundtrip failed. See ${summaryPath}.`);
  }

  console.log(`SIGNOUT_ROUNDTRIP_SUMMARY=${summaryPath}`);
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});