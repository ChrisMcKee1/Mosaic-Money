import { createUIMessageStream, createUIMessageStreamResponse } from "ai";
import { fetchApi } from "../../../../lib/api";

const RUN_POLL_INTERVAL_MS = 1500;
const RUN_POLL_TIMEOUT_MS = 45000;

export const maxDuration = 60;

function delay(milliseconds) {
  return new Promise((resolve) => {
    setTimeout(resolve, milliseconds);
  });
}

function extractTextFromMessage(message) {
  if (Array.isArray(message?.parts)) {
    const text = message.parts
      .filter((part) => part?.type === "text" && typeof part.text === "string")
      .map((part) => part.text)
      .join("")
      .trim();

    if (text.length > 0) {
      return text;
    }
  }

  if (typeof message?.content === "string") {
    return message.content.trim();
  }

  return "";
}

function resolveLatestUserMessage(messages) {
  if (!Array.isArray(messages)) {
    return { text: "", messageId: null };
  }

  for (let index = messages.length - 1; index >= 0; index -= 1) {
    const candidate = messages[index];
    if (candidate?.role !== "user") {
      continue;
    }

    const text = extractTextFromMessage(candidate);
    if (text.length > 0) {
      return { text, messageId: candidate.id || null };
    }
  }

  return { text: "", messageId: null };
}

function isTerminalRunStatus(status) {
  if (!status || typeof status !== "string") {
    return false;
  }

  const normalized = status.toLowerCase();
  return normalized === "completed"
    || normalized === "needsreview"
    || normalized === "failed"
    || normalized === "cancelled"
    || normalized === "canceled"
    || normalized === "incomplete"
    || normalized === "expired";
}

async function waitForRun(conversationId, correlationId) {
  if (!conversationId || !correlationId) {
    return null;
  }

  const startedAt = Date.now();
  let latestMatch = null;

  while (Date.now() - startedAt < RUN_POLL_TIMEOUT_MS) {
    const stream = await fetchApi(`/api/v1/agent/conversations/${conversationId}/stream`);
    const runs = Array.isArray(stream?.runs) ? stream.runs : [];

    const matchedRun = runs.find((run) => run?.correlationId === correlationId) ?? null;
    if (matchedRun) {
      latestMatch = matchedRun;
      if (isTerminalRunStatus(matchedRun.status)) {
        return matchedRun;
      }
    }

    await delay(RUN_POLL_INTERVAL_MS);
  }

  return latestMatch;
}

function buildAssistantReply(command, run) {
  if (command?.policyDisposition === "approval_required") {
    return "This request is queued and marked high-impact. Review the approval card before execution continues.";
  }

  if (!run) {
    return "I queued your request and started processing. Open the provenance tab for live run updates.";
  }

  const status = typeof run.status === "string" ? run.status.toLowerCase() : "";

  if (status === "completed") {
    if (typeof run.agentNoteSummary === "string" && run.agentNoteSummary.trim().length > 0) {
      return run.agentNoteSummary.trim();
    }

    if (typeof run.latestStageOutcomeSummary === "string" && run.latestStageOutcomeSummary.trim().length > 0) {
      return run.latestStageOutcomeSummary.trim();
    }

    return "The run completed successfully.";
  }

  if (status === "needsreview") {
    return "I completed initial processing but this outcome needs human review before it can continue.";
  }

  const failureDetails = [run.failureCode, run.failureRationale]
    .filter((value) => typeof value === "string" && value.trim().length > 0)
    .join(": ");

  if (failureDetails.length > 0) {
    return `The run did not complete successfully. ${failureDetails}`;
  }

  return `The run ended with status '${run.status || "unknown"}'. Check provenance for details.`;
}

function splitForStreaming(text) {
  if (!text || typeof text !== "string") {
    return [];
  }

  return text.split(/(\s+)/).filter((segment) => segment.length > 0);
}

export async function POST(request) {
  try {
    const payload = await request.json();
    const conversationId =
      typeof payload?.conversationId === "string" && payload.conversationId.trim().length > 0
        ? payload.conversationId.trim()
        : (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
            ? crypto.randomUUID()
            : `${Date.now()}-conversation`);

    const { text: userMessage, messageId } = resolveLatestUserMessage(payload?.messages);
    if (!userMessage) {
      return Response.json({ error: "A user message is required." }, { status: 400 });
    }

    const accepted = await fetchApi(`/api/v1/agent/conversations/${conversationId}/messages`, {
      method: "POST",
      body: JSON.stringify({
        message: userMessage,
        clientMessageId: messageId,
      }),
    });

    const run = accepted?.policyDisposition === "approval_required"
      ? null
      : await waitForRun(conversationId, accepted?.correlationId);
    const responseText = buildAssistantReply(accepted, run);

    const stream = createUIMessageStream({
      execute: async ({ writer }) => {
        writer.write({
          type: "data-command",
          data: {
            commandId: accepted?.commandId || null,
            correlationId: accepted?.correlationId || null,
            conversationId,
            policyDisposition: accepted?.policyDisposition || "advisory_only",
            queuedAtUtc: accepted?.queuedAtUtc || new Date().toISOString(),
            summary: userMessage,
          },
          transient: true,
        });

        if (run) {
          writer.write({
            type: "data-run",
            data: run,
            transient: true,
          });
        }

        const responseTextId = accepted?.commandId
          ? `assistant-${accepted.commandId}`
          : `assistant-${Date.now()}`;

        writer.write({ type: "text-start", id: responseTextId });
        for (const chunk of splitForStreaming(responseText)) {
          writer.write({ type: "text-delta", id: responseTextId, delta: chunk });
          await delay(15);
        }
        writer.write({ type: "text-end", id: responseTextId });
      },
      onError: () => "The assistant stream failed while preparing a response.",
    });

    return createUIMessageStreamResponse({ stream });
  } catch (error) {
    console.error("Assistant chat route failed:", error);
    const baseUrl = process.env.services__api__https__0 || process.env.services__api__http__0 || process.env.API_URL || "unknown";
    return Response.json({ error: String(error.message), stack: String(error.stack), fetchUrlAttempted: baseUrl }, { status: 500 });
  }
}
