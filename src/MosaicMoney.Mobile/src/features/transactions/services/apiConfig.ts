const configuredApiBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL?.trim();

// TODO(mm-mob-03): Replace env-based URL with AppHost service discovery bridge for mobile local runs.
export function getApiBaseUrl(): string {
  if (!configuredApiBaseUrl) {
    throw new Error(
      "EXPO_PUBLIC_API_BASE_URL is not configured. Set a non-secret API base URL for mobile (for example in local env).",
    );
  }

  return configuredApiBaseUrl.replace(/\/$/, "");
}
