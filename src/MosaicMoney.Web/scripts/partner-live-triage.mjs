import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { loadLocalTriageEnv } from './load-local-triage-env.mjs';

loadLocalTriageEnv();

const baseUrl = process.env.MM_TRIAGE_BASE_URL ?? 'http://localhost:53832';
const outputDir = path.resolve('../../artifacts/release-gates/mm-qa-04/live-triage');
const partners = [
  { key: 'partner-a', email: process.env.MM_PARTNER_A_EMAIL, password: process.env.MM_PARTNER_A_PASSWORD },
  { key: 'partner-b', email: process.env.MM_PARTNER_B_EMAIL, password: process.env.MM_PARTNER_B_PASSWORD },
];

const pathsToCheck = [
  '/',
  '/dashboard',
  '/accounts',
  '/transactions',
  '/needs-review',
  '/categories',
  '/investments',
  '/recurrings',
  '/settings',
  '/settings/household',
  '/onboarding/plaid',
];

function normalizeName(input) {
  const fallback = input === '/' ? 'root' : input;
  return fallback.replace(/[^a-z0-9-]/gi, '-').replace(/^-+|-+$/g, '').toLowerCase();
}

async function clickFirstVisible(page, selectors) {
  for (const selector of selectors) {
    const locator = page.locator(selector).first();

    if (!(await locator.count())) {
      continue;
    }

    try {
      if (await locator.isVisible({ timeout: 1200 })) {
        await locator.click({ timeout: 2500 });
        return true;
      }
    } catch {
    }
  }

  return false;
}

async function signIn(page, email, password) {
  await page.goto(`${baseUrl}/sign-in`, { waitUntil: 'domcontentloaded' });

  const identifierInputs = page.locator(
    'input[name="identifier"]:visible, input[type="email"]:visible, input[autocomplete="username"]:visible, input[id*="email" i]:visible, input[type="text"]:visible'
  );
  const passwordInputs = page.locator('input[type="password"]:visible, input[name="password"]:visible');

  await identifierInputs.first().waitFor({ state: 'visible', timeout: 25000 });
  await passwordInputs.first().waitFor({ state: 'visible', timeout: 25000 });

  for (let index = 0; index < (await identifierInputs.count()); index += 1) {
    await identifierInputs.nth(index).fill(email);
  }

  for (let index = 0; index < (await passwordInputs.count()); index += 1) {
    await passwordInputs.nth(index).fill(password);
  }

  const submitButtons = page.locator(
    'button:has-text("Continue"):visible, button[type="submit"]:visible, button:has-text("Sign In"):visible, button:has-text("Sign in"):visible'
  );

  const submitCount = await submitButtons.count();
  if (!submitCount) {
    throw new Error('Unable to find a visible sign-in submit button.');
  }

  await submitButtons.nth(submitCount - 1).click({ timeout: 5000 });
  await page.waitForTimeout(2500);
  await page.waitForLoadState('networkidle').catch(() => null);

  if (page.url().includes('/sign-in/factor-one')) {
    for (let index = 0; index < (await passwordInputs.count()); index += 1) {
      await passwordInputs.nth(index).fill(password);
    }

    const retryCount = await submitButtons.count();
    if (retryCount) {
      await submitButtons.nth(retryCount - 1).click({ timeout: 5000 });
      await page.waitForTimeout(2500);
      await page.waitForLoadState('networkidle').catch(() => null);
    }
  }
}

async function run() {
  await fs.mkdir(outputDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const summary = {
    baseUrl,
    timestampUtc: new Date().toISOString(),
    partners: [],
  };

  for (const partner of partners) {
    if (!partner.email || !partner.password) {
      summary.partners.push({
        key: partner.key,
        error: 'Missing credentials in environment variables',
      });
      continue;
    }

    const context = await browser.newContext();
    const page = await context.newPage();

    const partnerResult = {
      key: partner.key,
      email: partner.email,
      postLoginUrl: null,
      routes: [],
      signInError: null,
    };

    try {
      await signIn(page, partner.email, partner.password);
      partnerResult.postLoginUrl = page.url();

      for (const routePath of pathsToCheck) {
        const url = `${baseUrl}${routePath}`;
        let navStatus = null;
        let navError = null;
        let pageNotFoundSignal = false;

        try {
          const response = await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });
          navStatus = response?.status() ?? null;

          const bodyText = (await page.locator('body').innerText()).toLowerCase();
          pageNotFoundSignal = bodyText.includes('404') || bodyText.includes('not found');
        } catch (error) {
          navError = String(error);
        }

        const screenshotName = `${partner.key}-${normalizeName(routePath)}.png`;
        await page.screenshot({ path: path.join(outputDir, screenshotName), fullPage: true });

        partnerResult.routes.push({
          path: routePath,
          finalUrl: page.url(),
          status: navStatus,
          pageNotFoundSignal,
          navError,
          screenshot: `live-triage/${screenshotName}`,
        });
      }
    } catch (error) {
      partnerResult.signInError = String(error);
      await page.screenshot({ path: path.join(outputDir, `${partner.key}-signin-error.png`), fullPage: true });
    }

    await context.close();
    summary.partners.push(partnerResult);
  }

  await browser.close();

  const summaryPath = path.join(outputDir, 'summary.json');
  await fs.writeFile(summaryPath, JSON.stringify(summary, null, 2), 'utf8');
  console.log(`TRIAGE_SUMMARY=${summaryPath}`);
}

run().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
