import { auth } from "@clerk/nextjs/server";

/**
 * Server-side API fetch utility.
 * Uses Aspire-injected service URLs or a fallback API_URL environment variable.
 * Never hardcodes localhost.
 */
export function getApiBaseUrl() {
  // Aspire injects services__<name>__<endpoint>__0
  const aspireHttps = process.env.services__api__https__0;
  const aspireHttp = process.env.services__api__http__0;
  const fallbackUrl = process.env.API_URL;

  const baseUrl = aspireHttps || aspireHttp || fallbackUrl;

  if (!baseUrl) {
    throw new Error("API base URL is not configured. Ensure Aspire is running or API_URL is set.");
  }

  return baseUrl.replace(/\/$/, ""); // Remove trailing slash if present
}

async function getAuthorizationHeader() {
  if (!process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY) {
    return {};
  }

  try {
    const authContext = await auth();
    const token = await authContext.getToken();

    if (!token) {
      return {};
    }

    return { Authorization: `Bearer ${token}` };
  } catch (error) {
    console.error("Failed to resolve Clerk bearer token for API request.", error);
    return {};
  }
}

/**
 * Fetch wrapper for server components/route handlers.
 * @param {string} path - The API path (e.g., '/api/health')
 * @param {RequestInit} [options] - Fetch options
 */
export async function fetchApi(path, options = {}) {
  const baseUrl = getApiBaseUrl();
  const url = `${baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  const authorizationHeader = await getAuthorizationHeader();
  const householdUserId = process.env.MOSAIC_HOUSEHOLD_USER_ID?.trim();
  const identityHeader = householdUserId
    ? { "X-Mosaic-Household-User-Id": householdUserId }
    : {};

  const response = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...authorizationHeader,
      ...identityHeader,
      ...options.headers,
    },
  });

  if (!response.ok) {
    console.error(`API fetch failed for ${url}: ${response.status} ${response.statusText}`);
    throw new Error(`API fetch failed: ${response.status} ${response.statusText}`);
  }

  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    return response.json();
  }
  
  return response.text();
}
