import { chromium } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import { loadLocalTriageEnv } from "./load-local-triage-env.mjs";

loadLocalTriageEnv();

function resolveRequiredUrl(primaryEnvKey, fallbackEnvKey) {
  const primaryValue = process.env[primaryEnvKey]?.trim();
  if (primaryValue) {
    return primaryValue;
  }

  const fallbackValue = process.env[fallbackEnvKey]?.trim();
  if (fallbackValue) {
    return fallbackValue;
  }

  throw new Error(`Missing required base URL env. Set ${primaryEnvKey} or ${fallbackEnvKey}.`);
}

const webBaseUrl = resolveRequiredUrl("MM_TRIAGE_BASE_URL", "MM_WEB_BASE_URL");
const apiBaseUrl = resolveRequiredUrl("MM_API_BASE_URL", "API_URL");
const outputDir = path.resolve("../../artifacts/release-gates/mm-qa-04/live-triage");

const partners = [
  {
    key: "partner-a",
    email: process.env.MM_PARTNER_A_EMAIL,
    password: process.env.MM_PARTNER_A_PASSWORD,
    householdUserId: process.env.MM_PARTNER_A_HOUSEHOLD_USER_ID ?? "057b403b-389c-4f0a-a15b-70b0694dc354",
  },
  {
    key: "partner-b",
    email: process.env.MM_PARTNER_B_EMAIL,
    password: process.env.MM_PARTNER_B_PASSWORD,
    householdUserId: process.env.MM_PARTNER_B_HOUSEHOLD_USER_ID ?? "ab7d09db-7100-4322-9c79-25813e53f977",
  },
];

const searchQueries = ["kfc", "kfc combo", "fried chicken"];

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

async function getJson(requestContext, url, token, householdUserId) {
  const response = await requestContext.get(url, {
    failOnStatusCode: false,
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
    status: response.status(),
    body,
  };
}

async function setTheme(page, theme) {
  await page.goto(`${webBaseUrl}/settings`, { waitUntil: "domcontentloaded" });

  const candidateButtons = [
    page.locator("#main-content").getByRole("button", { name: new RegExp(`^${theme}$`, "i") }).first(),
    page.getByRole("button", { name: new RegExp(`^${theme}$`, "i") }).first(),
  ];

  let clicked = false;

  for (const button of candidateButtons) {
    if (await button.count()) {
      try {
        await button.click({ timeout: 2500 });
        clicked = true;
        break;
      } catch {
      }
    }
  }

  if (!clicked) {
    return false;
  }

  await page.waitForFunction((expectedTheme) => document.documentElement.dataset.theme === expectedTheme, theme, {
    timeout: 5000,
  }).catch(() => null);

  return true;
}

async function readRowDescriptions(page) {
  const rows = page.locator("div.divide-y > button p.text-sm.font-medium");
  const count = await rows.count();
  const descriptions = [];

  for (let index = 0; index < Math.min(count, 5); index += 1) {
    descriptions.push((await rows.nth(index).innerText()).trim());
  }

  return descriptions;
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
    timestampUtc: new Date().toISOString(),
    checks: [],
    success: false,
  };

  try {
    for (const partner of partners) {
      const context = await browser.newContext({ ignoreHTTPSErrors: true });
      const page = await context.newPage();

      const check = {
        partner: partner.key,
        postLoginUrl: null,
        tokenPresent: false,
        security: {
          pageUrl: null,
          backLinkVisible: false,
          screenshot: `live-triage/${partner.key}-security-page.png`,
        },
        platformCategoriesApi: {
          status: null,
          count: null,
          includesPlatformBaseline: false,
          sampleNames: [],
        },
        categoriesSettings: {
          pageUrl: null,
          platformTabVisible: false,
          platformLoadErrorVisible: false,
          createdCategoryName: null,
          createdCategoryVisible: false,
          screenshot: `live-triage/${partner.key}-settings-categories.png`,
        },
        transactionsSearchApi: [],
        transactionsSearchUi: [],
        categoriesDonut: [],
        plaidOnboarding: {
          getStartedClass: null,
          openPlaidLinkClass: null,
          screenshot: `live-triage/${partner.key}-plaid-onboarding.png`,
        },
        errors: [],
      };

      try {
        await signIn(page, partner.email, partner.password);
        check.postLoginUrl = page.url();

        const token = await getSessionToken(page);
        check.tokenPresent = !!token;
        if (!token) {
          throw new Error("Could not get Clerk session token after sign-in.");
        }

        // Security page: back-link presence + screenshot
        await page.goto(`${webBaseUrl}/settings/security`, { waitUntil: "domcontentloaded" });
        check.security.pageUrl = page.url();
        check.security.backLinkVisible = await page.getByRole("link", { name: "Back to Settings", exact: true }).first().isVisible().catch(() => false);
        await page.screenshot({ path: path.join(outputDir, `${partner.key}-security-page.png`), fullPage: true });

        // Platform categories API check for baseline visibility
        const platformCategories = await getJson(
          context.request,
          `${apiBaseUrl}/api/v1/categories?scope=Platform&includeArchived=false`,
          token,
          partner.householdUserId,
        );
        check.platformCategoriesApi.status = platformCategories.status;
        if (Array.isArray(platformCategories.body)) {
          check.platformCategoriesApi.count = platformCategories.body.length;
          check.platformCategoriesApi.sampleNames = platformCategories.body.slice(0, 10).map((x) => x?.name).filter(Boolean);
          check.platformCategoriesApi.includesPlatformBaseline = platformCategories.body.some(
            (x) => typeof x?.name === "string" && x.name.toLowerCase().includes("platform baseline"),
          );
        } else {
          check.platformCategoriesApi.count = 0;
        }

        // Category settings UI: platform tab health + create a category in mutable scope
        await page.goto(`${webBaseUrl}/settings/categories`, { waitUntil: "domcontentloaded" });
        check.categoriesSettings.pageUrl = page.url();

        const platformTab = page.getByRole("button", { name: /Platform Baseline/i }).first();
        check.categoriesSettings.platformTabVisible = await platformTab.isVisible().catch(() => false);

        if (check.categoriesSettings.platformTabVisible) {
          await platformTab.click({ timeout: 3000 }).catch(() => null);
          const bodyText = (await page.locator("body").innerText()).toLowerCase();
          check.categoriesSettings.platformLoadErrorVisible = bodyText.includes("unable to load platform categories right now");
        }

        const myCategoriesTab = page.getByRole("button", { name: /My Categories/i }).first();
        if (await myCategoriesTab.isVisible().catch(() => false)) {
          await myCategoriesTab.click({ timeout: 3000 }).catch(() => null);
        }

        const categoryName = `Smoke Category ${partner.key} ${new Date().toISOString().replace(/[:.]/g, "-")}`;
        const categoryInput = page.getByLabel("Category name").first();
        const createButton = page.getByRole("button", { name: /^Create$/ }).first();

        if (await categoryInput.isVisible().catch(() => false)) {
          await categoryInput.fill(categoryName);
          if (await createButton.isEnabled().catch(() => false)) {
            await createButton.click({ timeout: 5000 }).catch(() => null);
            await page.waitForTimeout(1500);
            check.categoriesSettings.createdCategoryName = categoryName;
            check.categoriesSettings.createdCategoryVisible = await page.getByText(categoryName, { exact: false }).first().isVisible().catch(() => false);
          }
        }

        await page.screenshot({ path: path.join(outputDir, `${partner.key}-settings-categories.png`), fullPage: true });

        // Transactions search variants: UI + API snapshot
        await page.goto(`${webBaseUrl}/transactions`, { waitUntil: "domcontentloaded" });
        const searchInput = page.getByPlaceholder("Search transactions...");

        for (const query of searchQueries) {
          await searchInput.fill(query);
          await page.waitForTimeout(1200);

          const uiDescriptions = await readRowDescriptions(page);
          check.transactionsSearchUi.push({
            query,
            topDescriptions: uiDescriptions,
            noMatchMessageVisible: await page.getByText("No transactions matched your search on this page.", { exact: true }).isVisible().catch(() => false),
          });

          const response = await getJson(
            context.request,
            `${apiBaseUrl}/api/v1/search/transactions?query=${encodeURIComponent(query)}&limit=10`,
            token,
            partner.householdUserId,
          );

          const apiDescriptions = Array.isArray(response.body)
            ? response.body.slice(0, 5).map((x) => x?.description).filter(Boolean)
            : [];

          check.transactionsSearchApi.push({
            query,
            status: response.status,
            resultCount: Array.isArray(response.body) ? response.body.length : 0,
            topDescriptions: apiDescriptions,
          });

          await page.screenshot({
            path: path.join(outputDir, `${partner.key}-transactions-search-${query.replace(/[^a-z0-9]+/gi, "-").toLowerCase()}.png`),
            fullPage: true,
          });
        }

        // Donut chart screenshots in light + dark themes
        for (const theme of ["light", "dark"]) {
          const themeApplied = await setTheme(page, theme);
          await page.goto(`${webBaseUrl}/categories`, { waitUntil: "domcontentloaded" });

          const chartVisible = await page.locator(".apexcharts-canvas").first().isVisible().catch(() => false);
          let hasBlackStroke = false;
          const firstSliceStroke = await page.locator("path.apexcharts-pie-area").first().getAttribute("stroke").catch(() => null);
          if (firstSliceStroke && firstSliceStroke.toLowerCase().startsWith("#000")) {
            hasBlackStroke = true;
          }

          check.categoriesDonut.push({
            theme,
            themeApplied,
            chartVisible,
            firstSliceStroke,
            hasBlackStroke,
            screenshot: `live-triage/${partner.key}-categories-${theme}.png`,
          });

          await page.screenshot({ path: path.join(outputDir, `${partner.key}-categories-${theme}.png`), fullPage: true });
        }

        // Plaid onboarding button style snapshot
        await page.goto(`${webBaseUrl}/onboarding/plaid`, { waitUntil: "domcontentloaded" });
        const getStartedButton = page.getByRole("button", { name: "Get Started", exact: true }).first();
        check.plaidOnboarding.getStartedClass = await getStartedButton.getAttribute("class").catch(() => null);

        if (await getStartedButton.isVisible().catch(() => false)) {
          await getStartedButton.click({ timeout: 5000 }).catch(() => null);
          await page.waitForTimeout(1500);
        }

        const openPlaidLinkButton = page.getByRole("button", { name: "Open Plaid Link", exact: true }).first();
        check.plaidOnboarding.openPlaidLinkClass = await openPlaidLinkButton.getAttribute("class").catch(() => null);

        await page.screenshot({ path: path.join(outputDir, `${partner.key}-plaid-onboarding.png`), fullPage: true });
      } catch (error) {
        check.errors.push(String(error));
      } finally {
        await context.close();
      }

      summary.checks.push(check);
    }

    summary.success = summary.checks.every((x) => x.errors.length === 0);
  } finally {
    await browser.close();
  }

  const summaryPath = path.join(outputDir, "partner-regression-smoke-summary.json");
  await fs.writeFile(summaryPath, JSON.stringify(summary, null, 2), "utf8");

  if (!summary.success) {
    throw new Error(`Partner regression smoke failed. See ${summaryPath}.`);
  }

  console.log(`PARTNER_REGRESSION_SMOKE_SUMMARY=${summaryPath}`);
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
