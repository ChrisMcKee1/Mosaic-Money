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

    const data = await fetchApi(`/api/v1/agent/conversations/${conversationId}/messages`, {
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

    const data = await fetchApi(`/api/v1/agent/conversations/${conversationId}/approvals/${approvalId}`, {
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
    const data = await fetchApi(`/api/v1/agent/conversations/${conversationId}/stream${sinceUtc}`);
    return { success: true, data };
  } catch (error) {
    console.error("Failed to fetch agent stream.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

export async function getAgentPromptLibrary(input = {}) {
  try {
    const query = input?.query?.trim();
    const querySuffix = query ? `?query=${encodeURIComponent(query)}` : "";
    const data = await fetchApi(`/api/v1/agent/prompts${querySuffix}`);
    return { success: true, data };
  } catch (error) {
    console.error("Failed to fetch reusable prompt library.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

export async function generateAgentPromptSuggestion(input) {
  try {
    const mode = input?.mode;
    if (!mode) {
      return { success: false, error: "Generation mode is required.", data: null };
    }

    const conversationMessages = Array.isArray(input?.conversationMessages)
      ? input.conversationMessages
          .map((message) => ({
            role: String(message?.role ?? "user").trim() || "user",
            text: String(message?.text ?? "").trim(),
          }))
          .filter((message) => message.text.length > 0)
          .slice(-20)
      : [];

    const payload = {
      mode,
      initialPrompt: input?.initialPrompt?.trim() || null,
      includePromptText: input?.includePromptText !== false,
      conversationMessages,
    };

    const data = await fetchApi("/api/v1/agent/prompts/generate", {
      method: "POST",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to generate reusable prompt suggestion.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

export async function createAgentPrompt(input) {
  try {
    const title = input?.title?.trim();
    const promptText = input?.promptText?.trim();
    if (!title || !promptText) {
      return { success: false, error: "Title and prompt text are required.", data: null };
    }

    const payload = {
      title,
      promptText,
      isFavorite: input?.isFavorite === true,
    };

    const data = await fetchApi("/api/v1/agent/prompts", {
      method: "POST",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to create reusable prompt.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

export async function updateAgentPrompt(input) {
  try {
    const id = input?.id;
    if (!id) {
      return { success: false, error: "Prompt id is required.", data: null };
    }

    const payload = {};
    if (Object.prototype.hasOwnProperty.call(input ?? {}, "title")) {
      payload.title = input?.title ?? null;
    }

    if (Object.prototype.hasOwnProperty.call(input ?? {}, "promptText")) {
      payload.promptText = input?.promptText ?? null;
    }

    if (Object.prototype.hasOwnProperty.call(input ?? {}, "isFavorite")) {
      payload.isFavorite = input?.isFavorite;
    }

    const data = await fetchApi(`/api/v1/agent/prompts/${id}`, {
      method: "PATCH",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to update reusable prompt.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

export async function deleteAgentPrompt(input) {
  try {
    const id = input?.id;
    if (!id) {
      return { success: false, error: "Prompt id is required." };
    }

    await fetchApi(`/api/v1/agent/prompts/${id}`, {
      method: "DELETE",
    });

    return { success: true };
  } catch (error) {
    console.error("Failed to delete reusable prompt.", error);
    return { success: false, error: normalizeError(error) };
  }
}

export async function recordAgentPromptUse(input) {
  try {
    const id = input?.id;
    if (!id) {
      return { success: false, error: "Prompt id is required.", data: null };
    }

    const payload = {
      conversationId: input?.conversationId || null,
    };

    const data = await fetchApi(`/api/v1/agent/prompts/${id}/use`, {
      method: "POST",
      body: JSON.stringify(payload),
    });

    return { success: true, data };
  } catch (error) {
    console.error("Failed to record prompt usage.", error);
    return { success: false, error: normalizeError(error), data: null };
  }
}

