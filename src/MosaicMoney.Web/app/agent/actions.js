"use server";

import { fetchApi } from "../../lib/api";

function normalizeError(error) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return "Agent request failed.";
}

export async function postAgentMessage(input) {
  try {
    const conversationId = input?.conversationId;
    const message = input?.message?.trim();

    if (!conversationId || !message) {
      return { success: false, error: "Conversation and message are required." };
    }

    const payload = {
      message,
      userNote: input?.userNote?.trim() || null,
      clientMessageId: input?.clientMessageId || null,
    };

    const data = await fetchApi(`/api/v1/assistant/conversations/${conversationId}/messages`, {
      method: "POST",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to post agent message.", error);
    return { success: false, error: normalizeError(error) };
  }
}

export async function submitAgentApproval(input) {
  try {
    const conversationId = input?.conversationId;
    const approvalId = input?.approvalId;
    const decision = input?.decision;

    if (!conversationId || !approvalId || !decision) {
      return { success: false, error: "Conversation, approval id, and decision are required." };
    }

    const payload = {
      decision,
      rationale: input?.rationale?.trim() || null,
      clientApprovalId: input?.clientApprovalId || null,
    };

    const data = await fetchApi(`/api/v1/assistant/conversations/${conversationId}/approvals/${approvalId}`, {
      method: "POST",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to submit agent approval.", error);
    return { success: false, error: normalizeError(error) };
  }
}

export async function getAgentConversationStream(input) {
  try {
    const conversationId = input?.conversationId;
    if (!conversationId) {
      return { success: false, error: "Conversation id is required.", data: null };
    }

    const sinceUtc = input?.sinceUtc ? `?sinceUtc=${encodeURIComponent(input.sinceUtc)}` : "";
    const data = await fetchApi(`/api/v1/assistant/conversations/${conversationId}/stream${sinceUtc}`);
    return { success: true, data };
  } catch (error) {
    console.error("Failed to fetch agent stream.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}
