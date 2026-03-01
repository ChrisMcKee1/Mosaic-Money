import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadLocalTriageEnv } from './load-local-triage-env.mjs';

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

  return 'http://localhost:53832';
}

const baseUrl = resolveRequiredUrl('MM_TRIAGE_BASE_URL', 'MM_WEB_BASE_URL');
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const outputDir = path.resolve(scriptDir, '..', '..', '..', 'artifacts', 'release-gates', 'mm-qa-04', 'live-agent-panel');

const partners = [
  { key: 'partner-a', email: process.env.MM_PARTNER_A_EMAIL, password: process.env.MM_PARTNER_A_PASSWORD },
  { key: 'partner-b', email: process.env.MM_PARTNER_B_EMAIL, password: process.env.MM_PARTNER_B_PASSWORD },
];

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

  const summary = {
    baseUrl,
    timestampUtc: new Date().toISOString(),
    partners: [],
  };

  for (const partner of partners) {
    if (!partner.email || !partner.password) {
      summary.partners.push({
        key: partner.key,
        status: 'failed',
        error: 'Missing credentials in environment variables',
      });
      continue;
    }

    const browser = await chromium.launch({ 
      headless: true,
      args: ['--disable-gpu', '--disable-dev-shm-usage', '--no-sandbox']
    });
    const context = await browser.newContext({
      viewport: { width: 1440, height: 1600 },
    });
    const page = await context.newPage();
    
    page.on('console', msg => console.log(`[PAGE LOG] ${msg.text()}`));
    page.on('pageerror', err => console.error(`[PAGE ERROR] ${err.message}`));
    page.on('close', () => console.log('[PAGE CLOSED]'));
    page.on('crash', () => console.log('[PAGE CRASHED]'));
    
    page.on('response', async (response) => {
      if (response.url().includes('/api/agent/chat')) {
        console.log(`[API RESPONSE] ${response.status()}`);
        try {
          const body = await response.text();
          console.log(`[API BODY] ${body}`);
        } catch (e) { }
      }
    });

    const partnerResult = {
      key: partner.key,
      status: 'started',
      signInError: null,
      panelError: null,
      responseReceived: false,
      chatHttpStatus: null,
      chatOutcome: null,
      chatResponseSnippet: null,
      sendButtonEnabledBeforeClick: null,
      inputValueLength: 0,
    };

    try {
      console.log(`Starting ${partner.key}...`);
      await signIn(page, partner.email, partner.password);
      
      console.log(`(${partner.key}) post-login URL:`, page.url());
      await page.waitForURL(url => !url.href.includes('sign-in') && !url.href.includes('sso-callback'), { timeout: 30000 });
      console.log(`(${partner.key}) navigated to:`, page.url());

      await page.locator('aside.w-64').first().waitFor({ state: 'visible', timeout: 30000 });
      await page.waitForTimeout(2000); // give the page a moment to stabilize

      console.log(`(${partner.key}) main content visible.`);
      await page.screenshot({ path: path.join(outputDir, `${partner.key}-main-content.png`), fullPage: true });
      
      // Open Agent Panel
      const toggleButton = page.locator('button[aria-label="Open agent"]');
      await toggleButton.waitFor({ state: 'visible', timeout: 30000 });
      console.log(`(${partner.key}) toggle button visible.`);
      await toggleButton.click();

      const panel = page.locator('section[aria-label="Global agent panel"]');
      await panel.waitFor({ state: 'visible', timeout: 10000 });

      // Look for the Agent Panel text input
      const msgInput = panel.locator('#agent-input');
      await msgInput.waitFor({ state: 'visible', timeout: 5000 });

      // Type a safe prompt
      await msgInput.fill('Can you summarize my recent activity?');
      partnerResult.inputValueLength = (await msgInput.inputValue()).length;
      await page.screenshot({ path: path.join(outputDir, `${partner.key}-before-send.png`), fullPage: true });
      
      // Click Send
      const sendButton = panel.locator('button[type="submit"]', { hasText: 'Send' }).first();
      await sendButton.waitFor({ state: 'visible', timeout: 10000 });
      partnerResult.sendButtonEnabledBeforeClick = await sendButton.isEnabled();

      await sendButton.scrollIntoViewIfNeeded();
      await sendButton.click({ timeout: 10000 });

      await page.screenshot({ path: path.join(outputDir, `${partner.key}-after-send.png`), fullPage: true });

      const agentMessage = page.locator('article').filter({ hasText: 'agent' }).first();
      const queueError = page.locator('text=The agent could not queue that message. Please retry.').first();
      const streamError = page.locator('text=Something went wrong while streaming the agent response.').first();

      const outcome = await Promise.race([
        agentMessage.waitFor({ state: 'visible', timeout: 45000 }).then(() => 'agent-message'),
        queueError.waitFor({ state: 'visible', timeout: 45000 }).then(() => 'queue-error'),
        streamError.waitFor({ state: 'visible', timeout: 45000 }).then(() => 'stream-error'),
      ]).catch(() => 'timeout');

      partnerResult.chatOutcome = outcome;
      await page.screenshot({ path: path.join(outputDir, `${partner.key}-post-outcome.png`), fullPage: true });

      if (outcome === 'agent-message') {
        partnerResult.status = 'passed';
        partnerResult.responseReceived = true;
      } else {
        partnerResult.status = 'failed';
        partnerResult.error = `Chat outcome '${outcome}'.`;
      }

    } catch (err) {
      console.error(`Error for ${partner.key}:`, err);
      // Try to capture error banners if present
      let bannerText = null;
      try {
        if (!page.isClosed()) {
          const errorBanner = page.locator('.mx-4.mt-3.rounded-lg.border.border-\\[var\\(--color-negative\\)\\/30\\]');
          if (await errorBanner.count() > 0) {
            bannerText = await errorBanner.first().innerText();
          }
          await page.screenshot({ path: path.join(outputDir, `${partner.key}-failure.png`), fullPage: true });
        }
      } catch (innerErr) {
        console.error('Error while handling failure:', innerErr);
      }
      
      partnerResult.status = 'failed';
      partnerResult.error = err.message;
      partnerResult.bannerText = bannerText;
    }

    summary.partners.push(partnerResult);
    try { await context.close(); } catch {}
    try { await browser.close(); } catch {}
  }

  await fs.writeFile(
    path.join(outputDir, 'summary.json'),
    JSON.stringify(summary, null, 2)
  );

  console.log('Triage complete.', JSON.stringify(summary, null, 2));

  const allPassed = summary.partners.every(p => p.status === 'passed');
  if (!allPassed) {
    process.exit(1);
  }
}

run().catch((err) => {
  console.error('Fatal:', err);
  process.exit(1);
});

