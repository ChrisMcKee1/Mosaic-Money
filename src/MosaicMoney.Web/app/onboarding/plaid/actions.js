"use server";

import { fetchApi } from "../../../lib/api";

export async function createLinkToken(clientUserId) {
  try {
    const response = await fetchApi("/api/v1/plaid/link-tokens", {
      method: "POST",
      body: JSON.stringify({
        clientUserId,
      }),
    });
    return { success: true, data: response };
  } catch (error) {
    console.error("Failed to create link token:", error);
    return { success: false, error: error.message };
  }
}

export async function logLinkSessionEvent(sessionId, eventType, source = "web", metadata = null) {
  try {
    const response = await fetchApi(`/api/v1/plaid/link-sessions/${sessionId}/events`, {
      method: "POST",
      body: JSON.stringify({
        eventType,
        source,
        clientMetadataJson: metadata ? JSON.stringify(metadata) : null,
      }),
    });
    return { success: true, data: response };
  } catch (error) {
    console.error("Failed to log link session event:", error);
    return { success: false, error: error.message };
  }
}

export async function exchangePublicToken(publicToken, linkSessionId, institutionId = null, metadata = null) {
  try {
    const response = await fetchApi("/api/v1/plaid/public-token-exchange", {
      method: "POST",
      body: JSON.stringify({
        publicToken,
        linkSessionId,
        institutionId,
        clientMetadataJson: metadata ? JSON.stringify(metadata) : null,
      }),
    });
    return { success: true, data: response };
  } catch (error) {
    console.error("Failed to exchange public token:", error);
    return { success: false, error: error.message };
  }
}
