import { expect } from "@playwright/test";

const mockApiPort = Number(process.env.MM_E2E_MOCK_API_PORT ?? 5055);
const mockApiBaseUrl = `http://127.0.0.1:${mockApiPort}`;

export async function resetMockApi(request) {
  const response = await request.post(`${mockApiBaseUrl}/__e2e/reset`);
  expect(response.ok()).toBeTruthy();
}

export async function setMockScenario(request, flags) {
  const response = await request.post(`${mockApiBaseUrl}/__e2e/scenario`, {
    data: flags,
  });
  expect(response.ok()).toBeTruthy();
}
