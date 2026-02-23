import { defineConfig, devices } from "@playwright/test";

const webPort = Number(process.env.MM_E2E_WEB_PORT ?? 3100);
const mockApiPort = Number(process.env.MM_E2E_MOCK_API_PORT ?? 5055);
const baseURL = `http://127.0.0.1:${webPort}`;
const mockApiURL = `http://127.0.0.1:${mockApiPort}`;

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 45_000,
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 2 : 0,
  expect: {
    timeout: 8_000,
  },
  reporter: process.env.CI
    ? [["github"], ["html", { open: "never" }]]
    : [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: [
    {
      command: "node tests/e2e/mock-api-server.mjs",
      url: `${mockApiURL}/__e2e/ready`,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      env: {
        MM_E2E_MOCK_API_PORT: String(mockApiPort),
      },
    },
    {
      command: `npm run dev -- --port ${webPort}`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: {
        API_URL: mockApiURL,
        NEXT_TELEMETRY_DISABLED: "1",
      },
    },
  ],
  projects: [
    {
      name: "desktop-chromium",
      use: {
        ...devices["Desktop Chrome"],
      },
    },
    {
      name: "mobile-chromium",
      use: {
        ...devices["Pixel 7"],
      },
    },
  ],
});
