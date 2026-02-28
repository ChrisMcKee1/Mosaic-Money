"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Bot,
  Clock3,
  MessageSquare,
  PanelRightClose,
  PanelRightOpen,
  Send,
  ShieldAlert,
  Sparkles,
  User,
} from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import {
  getAssistantConversationStream,
  postAssistantMessage,
  submitAssistantApproval,
} from "../../app/assistant/actions";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

const POLL_INTERVAL_MS = 7000;

function createConversationId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `${Date.now()}-assistant-conversation`;
}

function formatTime(value) {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

function statusClassName(status) {
  if (!status) {
    return "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] border-[var(--color-border)]";
  }

  const normalized = status.toLowerCase();
  if (normalized === "completed") {
    return "bg-[var(--color-positive-bg)] text-[var(--color-positive)] border-[var(--color-positive)]/30";
  }

  if (normalized === "needsreview" || normalized === "failed") {
    return "bg-[var(--color-warning-bg)] text-[var(--color-warning)] border-[var(--color-warning)]/30";
  }

  if (normalized === "running") {
    return "bg-[var(--color-primary)]/15 text-[var(--color-primary)] border-[var(--color-primary)]/30";
  }

  return "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] border-[var(--color-border)]";
}

export function GlobalAgentPanel() {
  const [isOpen, setIsOpen] = useState(false);
  const [activeTab, setActiveTab] = useState("conversation");
  const [conversationId, setConversationId] = useState("");
  const [inputValue, setInputValue] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState("");

  const [messages, setMessages] = useState([]);
  const [approvalCards, setApprovalCards] = useState([]);
  const [timelineRuns, setTimelineRuns] = useState([]);

  useEffect(() => {
    try {
      const stored = localStorage.getItem("mosaic-assistant-conversation-id");
      const nextId = stored && stored.length > 0 ? stored : createConversationId();
      localStorage.setItem("mosaic-assistant-conversation-id", nextId);
      setConversationId(nextId);
    } catch {
      setConversationId(createConversationId());
    }
  }, []);

  async function refreshStream() {
    if (!conversationId) {
      return;
    }

    setIsRefreshing(true);
    const result = await getAssistantConversationStream({ conversationId });
    setIsRefreshing(false);

    if (!result.success) {
      setError(result.error || "Could not refresh assistant timeline.");
      return;
    }

    const runs = result?.data?.runs || [];
    setTimelineRuns(runs);
  }

  useEffect(() => {
    if (!isOpen || !conversationId) {
      return;
    }

    void refreshStream();
    const timer = setInterval(() => {
      void refreshStream();
    }, POLL_INTERVAL_MS);

    return () => clearInterval(timer);
  }, [isOpen, conversationId]);

  const pendingApprovals = useMemo(
    () => approvalCards.filter((card) => card.status === "pending"),
    [approvalCards],
  );

  async function handleSendMessage(event) {
    event.preventDefault();

    const message = inputValue.trim();
    if (!message || !conversationId || isSubmitting) {
      return;
    }

    setError("");
    setIsSubmitting(true);

    const optimisticMessage = {
      id: `${Date.now()}-local`,
      role: "user",
      text: message,
      createdAt: new Date().toISOString(),
    };

    setMessages((previous) => [...previous, optimisticMessage]);
    setInputValue("");

    const result = await postAssistantMessage({
      conversationId,
      message,
      clientMessageId: optimisticMessage.id,
    });

    setIsSubmitting(false);

    if (!result.success) {
      setError(result.error || "Assistant message failed.");
      setMessages((previous) => [
        ...previous,
        {
          id: `${Date.now()}-error`,
          role: "system",
          text: "The assistant could not queue that message. Please retry.",
          createdAt: new Date().toISOString(),
          tone: "error",
        },
      ]);
      return;
    }

    const accepted = result.data;
    const policyDisposition = accepted?.policyDisposition || "advisory_only";

    setMessages((previous) => [
      ...previous,
      {
        id: `${accepted.commandId}-queued`,
        role: "assistant",
        text:
          policyDisposition === "approval_required"
            ? "This request is marked high-impact. Review and approve before execution."
            : "Queued. I will stream run updates as the workflow progresses.",
        createdAt: accepted?.queuedAtUtc || new Date().toISOString(),
        tone: policyDisposition === "approval_required" ? "warning" : "normal",
      },
    ]);

    if (policyDisposition === "approval_required") {
      setApprovalCards((previous) => [
        {
          id: accepted.commandId,
          commandId: accepted.commandId,
          conversationId: accepted.conversationId,
          status: "pending",
          title: "High-impact action requires approval",
          summary: message,
          createdAt: accepted.queuedAtUtc || new Date().toISOString(),
        },
        ...previous,
      ]);
    }

    void refreshStream();
  }

  async function handleApprovalDecision(cardId, decision) {
    const card = approvalCards.find((entry) => entry.id === cardId);
    if (!card || card.status !== "pending") {
      return;
    }

    const confirmText = decision === "Approve"
      ? "Approve this high-impact assistant action?"
      : "Reject this high-impact assistant action?";

    if (typeof window !== "undefined" && !window.confirm(confirmText)) {
      return;
    }

    setError("");
    setApprovalCards((previous) =>
      previous.map((entry) =>
        entry.id === cardId ? { ...entry, status: "submitting" } : entry,
      ),
    );

    const result = await submitAssistantApproval({
      conversationId,
      approvalId: card.commandId,
      decision,
      clientApprovalId: `${cardId}-${decision.toLowerCase()}`,
      rationale: decision === "Approve" ? "Approved by user in web assistant panel." : "Rejected by user in web assistant panel.",
    });

    if (!result.success) {
      setError(result.error || "Approval request failed.");
      setApprovalCards((previous) =>
        previous.map((entry) =>
          entry.id === cardId ? { ...entry, status: "pending" } : entry,
        ),
      );
      return;
    }

    setApprovalCards((previous) =>
      previous.map((entry) =>
        entry.id === cardId
          ? { ...entry, status: decision === "Approve" ? "approved" : "rejected" }
          : entry,
      ),
    );

    setMessages((previous) => [
      ...previous,
      {
        id: `${cardId}-${decision.toLowerCase()}-system`,
        role: "system",
        text: decision === "Approve" ? "Approval submitted." : "Rejection submitted.",
        createdAt: new Date().toISOString(),
        tone: "normal",
      },
    ]);

    void refreshStream();
  }

  const panelBody = (
    <>
      <header className="border-b border-[var(--color-border)] px-4 py-3">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-wider text-[var(--color-text-muted)]">Mosaic Assistant</p>
            <h2 className="text-base font-semibold text-[var(--color-text-main)]">Policy-aware runtime assistant</h2>
          </div>
          <button
            type="button"
            onClick={() => setIsOpen(false)}
            className="rounded-md border border-[var(--color-border)] p-2 text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-[var(--color-text-main)]"
            aria-label="Close assistant"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-3 flex gap-2">
          <button
            type="button"
            className={cn(
              "rounded-md px-3 py-1.5 text-xs font-semibold transition-colors",
              activeTab === "conversation"
                ? "bg-[var(--color-primary)]/20 text-[var(--color-primary)]"
                : "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]",
            )}
            onClick={() => setActiveTab("conversation")}
          >
            <MessageSquare className="mr-1 inline h-3.5 w-3.5" />
            Conversation
          </button>
          <button
            type="button"
            className={cn(
              "rounded-md px-3 py-1.5 text-xs font-semibold transition-colors",
              activeTab === "timeline"
                ? "bg-[var(--color-primary)]/20 text-[var(--color-primary)]"
                : "bg-[var(--color-surface-hover)] text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]",
            )}
            onClick={() => setActiveTab("timeline")}
          >
            <Clock3 className="mr-1 inline h-3.5 w-3.5" />
            Provenance
          </button>
        </div>
      </header>

      {error ? (
        <div className="mx-4 mt-3 rounded-lg border border-[var(--color-negative)]/30 bg-[var(--color-negative-bg)] px-3 py-2 text-xs text-[var(--color-negative)]">
          {error}
        </div>
      ) : null}

      {activeTab === "conversation" ? (
        <>
          <section className="border-b border-[var(--color-border)] px-4 py-3">
            <div className="flex items-center justify-between">
              <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Approval queue</p>
              <span className="rounded-full border border-[var(--color-border)] px-2 py-0.5 text-[10px] text-[var(--color-text-muted)]">
                {pendingApprovals.length} pending
              </span>
            </div>

            <div className="mt-2 space-y-2 max-h-40 overflow-auto pr-1">
              {approvalCards.length === 0 ? (
                <p className="text-xs text-[var(--color-text-muted)]">No high-impact actions waiting for review.</p>
              ) : (
                approvalCards.map((card) => (
                  <article
                    key={card.id}
                    className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-3"
                  >
                    <p className="text-xs font-semibold text-[var(--color-text-main)]">{card.title}</p>
                    <p className="mt-1 text-xs text-[var(--color-text-muted)] line-clamp-2">{card.summary}</p>
                    <p className="mt-1 text-[10px] text-[var(--color-text-subtle)]">{formatTime(card.createdAt)}</p>

                    <div className="mt-2 flex items-center justify-between">
                      <span
                        className={cn(
                          "rounded-full border px-2 py-0.5 text-[10px]",
                          card.status === "approved"
                            ? "border-[var(--color-positive)]/30 text-[var(--color-positive)]"
                            : card.status === "rejected"
                              ? "border-[var(--color-negative)]/30 text-[var(--color-negative)]"
                              : card.status === "submitting"
                                ? "border-[var(--color-primary)]/30 text-[var(--color-primary)]"
                                : "border-[var(--color-warning)]/30 text-[var(--color-warning)]",
                        )}
                      >
                        {card.status}
                      </span>

                      {card.status === "pending" ? (
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => handleApprovalDecision(card.id, "Approve")}
                            className="rounded-md bg-[var(--color-approve-bg)] px-2 py-1 text-[10px] font-semibold text-[var(--color-approve-text)] hover:bg-[var(--color-approve-bg-hover)]"
                          >
                            Approve
                          </button>
                          <button
                            type="button"
                            onClick={() => handleApprovalDecision(card.id, "Reject")}
                            className="rounded-md border border-[var(--color-reject-border)] px-2 py-1 text-[10px] font-semibold text-[var(--color-reject-text)] hover:bg-[var(--color-reject-bg-hover)]"
                          >
                            Reject
                          </button>
                        </div>
                      ) : null}
                    </div>
                  </article>
                ))
              )}
            </div>
          </section>

          <section className="flex-1 overflow-auto px-4 py-3">
            <div className="space-y-3 pb-6">
              {messages.length === 0 ? (
                <div className="rounded-lg border border-dashed border-[var(--color-border)] p-4 text-xs text-[var(--color-text-muted)]">
                  <p className="font-semibold text-[var(--color-text-main)]">Start a guided conversation</p>
                  <p className="mt-1">Ask for help with categorization, review routing, or transaction context. High-impact requests will require approval.</p>
                </div>
              ) : (
                messages.map((message) => (
                  <article
                    key={message.id}
                    className={cn(
                      "rounded-xl border px-3 py-2 text-sm",
                      message.role === "user"
                        ? "ml-8 border-[var(--color-primary)]/25 bg-[var(--color-primary)]/10"
                        : message.tone === "warning"
                          ? "mr-8 border-[var(--color-warning)]/30 bg-[var(--color-warning-bg)]"
                          : message.tone === "error"
                            ? "mr-8 border-[var(--color-negative)]/30 bg-[var(--color-negative-bg)]"
                            : "mr-8 border-[var(--color-border)] bg-[var(--color-surface-hover)]",
                    )}
                  >
                    <div className="mb-1 flex items-center gap-1 text-[10px] uppercase tracking-wide text-[var(--color-text-muted)]">
                      {message.role === "user" ? <User className="h-3 w-3" /> : <Bot className="h-3 w-3" />}
                      <span>{message.role}</span>
                      <span className="ml-auto normal-case tracking-normal">{formatTime(message.createdAt)}</span>
                    </div>
                    <p className="text-[var(--color-text-main)]">{message.text}</p>
                  </article>
                ))
              )}
            </div>
          </section>

          <form onSubmit={handleSendMessage} className="border-t border-[var(--color-border)] px-4 py-3">
            <label htmlFor="assistant-input" className="sr-only">
              Ask assistant
            </label>
            <div className="flex items-end gap-2">
              <textarea
                id="assistant-input"
                value={inputValue}
                onChange={(event) => setInputValue(event.target.value)}
                placeholder="Ask assistant..."
                rows={2}
                className="w-full resize-none rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] focus:border-[var(--color-primary)] focus:outline-none"
              />
              <button
                type="submit"
                disabled={isSubmitting || !conversationId || !inputValue.trim()}
                className="inline-flex items-center gap-1 rounded-lg bg-[var(--color-primary)] px-3 py-2 text-xs font-semibold text-[var(--color-button-ink)] hover:bg-[var(--color-primary-hover)] disabled:cursor-not-allowed disabled:opacity-60"
              >
                <Send className="h-3.5 w-3.5" />
                {isSubmitting ? "Sending" : "Send"}
              </button>
            </div>
          </form>
        </>
      ) : (
        <section className="flex-1 overflow-auto px-4 py-4">
          <div className="mb-3 flex items-center justify-between">
            <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Run timeline</p>
            <button
              type="button"
              onClick={() => void refreshStream()}
              className="rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)]"
            >
              {isRefreshing ? "Refreshing..." : "Refresh"}
            </button>
          </div>

          <div className="space-y-2 pb-6">
            {timelineRuns.length === 0 ? (
              <div className="rounded-lg border border-dashed border-[var(--color-border)] p-4 text-xs text-[var(--color-text-muted)]">
                No run provenance yet. Send a message to start workflow tracking.
              </div>
            ) : (
              timelineRuns.map((run) => (
                <article key={run.runId} className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-3">
                  <div className="mb-2 flex items-start justify-between gap-2">
                    <div>
                      <div className="flex flex-wrap items-center gap-1.5">
                        <p className="text-xs font-semibold text-[var(--color-text-main)]">{run.triggerSource}</p>
                        {run.agentName ? (
                          <span className="rounded bg-[var(--color-primary)]/10 px-1.5 py-0.5 text-[9px] font-medium text-[var(--color-primary)]">
                            {run.agentName}{run.agentSource ? ` (${run.agentSource})` : ""}
                          </span>
                        ) : null}
                      </div>
                      <p className="mt-0.5 text-[10px] text-[var(--color-text-subtle)] font-mono break-all">{run.correlationId}</p>
                    </div>
                    <span className={cn("rounded-full border px-2 py-0.5 text-[10px] font-semibold", statusClassName(run.status))}>
                      {run.status}
                    </span>
                  </div>

                  <div className="space-y-1 text-[11px] text-[var(--color-text-muted)]">
                    <p className="flex items-center gap-1">
                      <Clock3 className="h-3 w-3" />
                      Created {formatTime(run.createdAtUtc)}
                    </p>
                    <p className="flex items-center gap-1">
                      <Sparkles className="h-3 w-3" />
                      Last update {formatTime(run.lastModifiedAtUtc)}
                    </p>
                    {run.latestStageOutcomeSummary ? (
                      <div className="mt-1.5 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] p-2">
                        <p className="text-[10px] text-[var(--color-text-main)] break-words">{run.latestStageOutcomeSummary}</p>
                        {run.assignmentHint ? (
                          <p className="mt-1 border-t border-[var(--color-border)]/50 pt-1 text-[9px] text-[var(--color-text-subtle)] break-words">
                            <span className="font-semibold text-[var(--color-text-muted)]">Hint: </span>
                            {run.assignmentHint}
                          </p>
                        ) : null}
                      </div>
                    ) : null}
                    {run.failureCode ? (
                      <p className="flex items-start gap-1 text-[var(--color-warning)]">
                        <ShieldAlert className="mt-0.5 h-3 w-3" />
                        <span>
                          {run.failureCode}
                          {run.failureRationale ? `: ${run.failureRationale}` : ""}
                        </span>
                      </p>
                    ) : null}
                  </div>
                </article>
              ))
            )}
          </div>
        </section>
      )}
    </>
  );

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        className="fixed bottom-5 right-5 z-40 inline-flex items-center gap-2 rounded-full border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-2 text-sm font-semibold text-[var(--color-text-main)] shadow-xl hover:bg-[var(--color-surface-hover)]"
        aria-label={isOpen ? "Close assistant" : "Open assistant"}
      >
        {isOpen ? <PanelRightClose className="h-4 w-4" /> : <PanelRightOpen className="h-4 w-4" />}
        Assistant
      </button>

      <section
        aria-label="Global assistant panel"
        className={cn(
          "fixed right-0 top-0 z-50 h-full w-full max-w-md border-l border-[var(--color-border)] bg-[var(--color-surface)] shadow-2xl transition-transform duration-300",
          isOpen ? "translate-x-0" : "translate-x-full",
        )}
      >
        {panelBody}
      </section>
    </>
  );
}
