import { chromium } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import { loadLocalTriageEnv } from "./load-local-triage-env.mjs";

loadLocalTriageEnv();

const webBaseUrl = process.env.MM_TRIAGE_BASE_URL ?? "http://localhost:53832";
const apiBaseUrl = process.env.MM_API_BASE_URL ?? "http://localhost:5207";
const outputDir = path.resolve("../../artifacts/release-gates/mm-qa-04/live-triage");

const householdId = process.env.MM_VISIBILITY_HOUSEHOLD_ID ?? "019c90db-bf42-7b32-89ec-cf6232220835";
const accountIds = {
  partnerAOnly: process.env.MM_VISIBILITY_ACCOUNT_A ?? "d4be7d80-2f77-4f5d-a6e6-6f5f702f7b2a",
  partnerBOnly: process.env.MM_VISIBILITY_ACCOUNT_B ?? "4b31d723-a791-4e6c-8906-79a194d12d26",
  joint: process.env.MM_VISIBILITY_ACCOUNT_JOINT ?? "e6df2a6e-3c4e-4deb-90aa-931ec5f5be88",
};

const partners = [
  {
    key: "partner-a",
    email: process.env.MM_PARTNER_A_EMAIL,
    password: process.env.MM_PARTNER_A_PASSWORD,
    householdUserId: process.env.MM_PARTNER_A_HOUSEHOLD_USER_ID ?? "057b403b-389c-4f0a-a15b-70b0694dc354",
    expectedVisibleAccountIds: [accountIds.partnerAOnly, accountIds.joint],
    expectedHiddenAccountIds: [accountIds.partnerBOnly],
  },
  {
    key: "partner-b",
    email: process.env.MM_PARTNER_B_EMAIL,
    password: process.env.MM_PARTNER_B_PASSWORD,
    householdUserId: process.env.MM_PARTNER_B_HOUSEHOLD_USER_ID ?? "ab7d09db-7100-4322-9c79-25813e53f977",
    expectedVisibleAccountIds: [accountIds.partnerBOnly, accountIds.joint],
    expectedHiddenAccountIds: [accountIds.partnerAOnly],
  },
];

async function fillAll(locator, value) {
  const count = await locator.count();
  for (let index = 0; index < count; index += 1) {
    await locator.nth(index).fill(value);
  }
}

async function signIn(page, email, password) {
  await page.goto(`${webBaseUrl}/sign-in`, { waitUntil: "domcontentloaded" });

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

async function getSessionToken(page) {
  return await page.evaluate(async () => {
    if (!window.Clerk?.session) {
      return null;
    }

    return await window.Clerk.session.getToken();
  });
}

async function getJson(url, token, householdUserId) {
  const response = await fetch(url, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Mosaic-Household-User-Id": householdUserId,
    },
  });

  const text = await response.text();
  let body = null;
  try {
    body = text ? JSON.parse(text) : null;
  } catch {
    body = text;
  }

  return {
    status: response.status,
    body,
  };
}

function evaluateVisibility(result, expectedVisibleAccountIds, expectedHiddenAccountIds) {
  const visibleIds = new Set((result.body ?? []).map((x) => x.accountId));
  const missingVisible = expectedVisibleAccountIds.filter((id) => !visibleIds.has(id));
  const leakedHidden = expectedHiddenAccountIds.filter((id) => visibleIds.has(id));

  return {
    visibleIds: Array.from(visibleIds),
    missingVisible,
    leakedHidden,
    pass: missingVisible.length === 0 && leakedHidden.length === 0,
  };
}

function evaluateTransactions(result, expectedVisibleAccountIds, expectedHiddenAccountIds) {
  const accountIdsSeen = new Set((result.body ?? []).map((x) => x.accountId));
  const hasVisible = expectedVisibleAccountIds.some((id) => accountIdsSeen.has(id));
  const leakedHidden = expectedHiddenAccountIds.filter((id) => accountIdsSeen.has(id));

  return {
    accountIdsSeen: Array.from(accountIdsSeen),
    hasVisible,
    leakedHidden,
    pass: hasVisible && leakedHidden.length === 0,
  };
}

async function run() {
  for (const partner of partners) {
    if (!partner.email || !partner.password || !partner.householdUserId) {
      throw new Error(`Missing credentials or household user id for ${partner.key}.`);
    }
  }

  await fs.mkdir(outputDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const summary = {
    webBaseUrl,
    apiBaseUrl,
    householdId,
    timestampUtc: new Date().toISOString(),
    results: [],
    success: false,
  };

  try {
    for (const partner of partners) {
      const context = await browser.newContext();
      const page = await context.newPage();

      const partnerResult = {
        key: partner.key,
        email: partner.email,
        postLoginUrl: null,
        tokenPresent: false,
        accountAccessResponseStatus: null,
        transactionsResponseStatus: null,
        accountAccessCheck: null,
        transactionsCheck: null,
        screenshot: `live-triage/${partner.key}-acl-api-validation.png`,
        error: null,
      };

      try {
        await signIn(page, partner.email, partner.password);
        partnerResult.postLoginUrl = page.url();

        const token = await getSessionToken(page);
        partnerResult.tokenPresent = !!token;

        if (!token) {
          throw new Error("Unable to resolve Clerk session token from browser context.");
        }

        const accountAccessResult = await getJson(
          `${apiBaseUrl}/api/v1/households/${householdId}/account-access`,
          token,
          partner.householdUserId,
        );
        partnerResult.accountAccessResponseStatus = accountAccessResult.status;

        const transactionsResult = await getJson(
          `${apiBaseUrl}/api/v1/transactions?page=1&pageSize=200`,
          token,
          partner.householdUserId,
        );
        partnerResult.transactionsResponseStatus = transactionsResult.status;

        if (accountAccessResult.status !== 200) {
          throw new Error(`Account-access API failed with status ${accountAccessResult.status}.`);
        }

        if (transactionsResult.status !== 200) {
          throw new Error(`Transactions API failed with status ${transactionsResult.status}.`);
        }

        partnerResult.accountAccessCheck = evaluateVisibility(
          accountAccessResult,
          partner.expectedVisibleAccountIds,
          partner.expectedHiddenAccountIds,
        );

        partnerResult.transactionsCheck = evaluateTransactions(
          transactionsResult,
          partner.expectedVisibleAccountIds,
          partner.expectedHiddenAccountIds,
        );

        await page.screenshot({ path: path.join(outputDir, `${partner.key}-acl-api-validation.png`), fullPage: true });

        if (!partnerResult.accountAccessCheck.pass || !partnerResult.transactionsCheck.pass) {
          throw new Error("ACL visibility assertions failed.");
        }
      } catch (error) {
        partnerResult.error = String(error);
      } finally {
        await context.close();
      }

      summary.results.push(partnerResult);
    }

    summary.success = summary.results.every((x) => !x.error);
  } finally {
    await browser.close();
  }

  const summaryPath = path.join(outputDir, "partner-acl-api-validation-summary.json");
  await fs.writeFile(summaryPath, JSON.stringify(summary, null, 2), "utf8");

  if (!summary.success) {
    throw new Error(`Partner ACL API validation failed. See ${summaryPath}.`);
  }

  console.log(`PARTNER_ACL_API_VALIDATION_SUMMARY=${summaryPath}`);
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});