"use client";

import { useEffect, useMemo, useState } from "react";
import { useChat } from "@ai-sdk/react";
import { DefaultChatTransport } from "ai";
import {
  Bot,
  Bookmark,
  Clock3,
  MessageSquare,
  Pencil,
  Plus,
  PanelRightClose,
  Send,
  Star,
  StarOff,
  ShieldAlert,
  Sparkles,
  Trash2,
  User,
} from "lucide-react";
import { clsx } from "clsx";
import { twMerge } from "tailwind-merge";
import {
  createAgentPrompt,
  deleteAgentPrompt,
  generateAgentPromptSuggestion,
  getAgentConversationStream,
  getAgentPromptLibrary,
  recordAgentPromptUse,
  submitAgentApproval,
  updateAgentPrompt,
} from "../../app/agent/actions";

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

const POLL_INTERVAL_MS = 7000;
const EMPTY_PROMPT_LIBRARY = {
  favorites: [],
  userPrompts: [],
  baselinePrompts: [],
};

function createConversationId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `${Date.now()}-agent-conversation`;
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

function extractMessageText(message) {
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
    return message.content;
  }

  if (typeof message?.text === "string") {
    return message.text;
  }

  return "";
}

export function GlobalAgentPanel() {
  const [isOpen, setIsOpen] = useState(false);
  const [activeTab, setActiveTab] = useState("conversation");
  const [conversationId, setConversationId] = useState("");
  const [inputValue, setInputValue] = useState("");
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [error, setError] = useState("");

  const [approvalCards, setApprovalCards] = useState([]);
  const [timelineRuns, setTimelineRuns] = useState([]);
  const [promptLibrary, setPromptLibrary] = useState(EMPTY_PROMPT_LIBRARY);
  const [promptSearch, setPromptSearch] = useState("");
  const [isPromptLibraryLoading, setIsPromptLibraryLoading] = useState(false);
  const [isPromptLibraryOpen, setIsPromptLibraryOpen] = useState(false);
  const [isPromptEditorOpen, setIsPromptEditorOpen] = useState(false);
  const [isPromptMutating, setIsPromptMutating] = useState(false);
  const [isPromptGenerating, setIsPromptGenerating] = useState(false);
  const [editingPromptId, setEditingPromptId] = useState(null);
  const [promptTitleDraft, setPromptTitleDraft] = useState("");
  const [promptBodyDraft, setPromptBodyDraft] = useState("");
  const [promptFavoriteDraft, setPromptFavoriteDraft] = useState(false);

  const {
    messages,
    setMessages,
    sendMessage,
    status,
    stop,
    error: chatError,
  } = useChat({
    id: conversationId || undefined,
    transport: new DefaultChatTransport({
      api: "/api/agent/chat",
    }),
    onData: (dataPart) => {
      if (dataPart?.type === "data-command") {
        const payload = dataPart?.data ?? {};
        if (payload?.policyDisposition === "approval_required" && payload?.commandId) {
          setApprovalCards((previous) => {
            if (previous.some((card) => card.commandId === payload.commandId)) {
              return previous;
            }

            return [
              {
                id: payload.commandId,
                commandId: payload.commandId,
                conversationId: payload.conversationId,
                status: "pending",
                title: "High-impact action requires approval",
                summary: payload.summary || "Approval required for this action.",
                createdAt: payload.queuedAtUtc || new Date().toISOString(),
              },
              ...previous,
            ];
          });
        }
      }

      if (dataPart?.type === "data-run") {
        void refreshStream();
      }
    },
  });

  const isSubmitting = status === "submitted" || status === "streaming";

  useEffect(() => {
    try {
      const stored = localStorage.getItem("mosaic-agent-conversation-id");
      const nextId = stored && stored.length > 0 ? stored : createConversationId();
      localStorage.setItem("mosaic-agent-conversation-id", nextId);
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
    const result = await getAgentConversationStream({ conversationId });
    setIsRefreshing(false);

    if (!result.success) {
      setError(result.error || "Could not refresh agent timeline.");
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

  async function refreshPromptLibrary(nextQuery = promptSearch) {
    setIsPromptLibraryLoading(true);
    const result = await getAgentPromptLibrary({ query: nextQuery });
    setIsPromptLibraryLoading(false);

    if (!result.success) {
      setError(result.error || "Could not load reusable prompts.");
      return;
    }

    setPromptLibrary(result.data ?? EMPTY_PROMPT_LIBRARY);
  }

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    void refreshPromptLibrary(promptSearch);
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const timer = setTimeout(() => {
      void refreshPromptLibrary(promptSearch);
    }, 250);

    return () => clearTimeout(timer);
  }, [isOpen, promptSearch]);

  const pendingApprovals = useMemo(
    () => approvalCards.filter((card) => card.status === "pending"),
    [approvalCards],
  );

  const latestRunStatus = useMemo(() => {
    const latestRun = timelineRuns[0];
    return latestRun?.status ?? null;
  }, [timelineRuns]);

  const conversationMessagesForGeneration = useMemo(() => {
    return messages
      .map((message) => ({
        role: message?.role ?? "user",
        text: extractMessageText(message).trim(),
      }))
      .filter((message) => message.text.length > 0)
      .slice(-20);
  }, [messages]);

  function resolveInitialPromptCandidate() {
    if (promptBodyDraft.trim().length > 0) {
      return promptBodyDraft.trim();
    }

    if (inputValue.trim().length > 0) {
      return inputValue.trim();
    }

    const firstUserMessage = conversationMessagesForGeneration.find((message) => message.role === "user");
    return firstUserMessage?.text ?? "";
  }

  async function queueMessage(messageText) {
    if (!messageText || !conversationId || isSubmitting) {
      return;
    }

    setError("");

    try {
      await sendMessage(
        { text: messageText },
        {
          body: {
            conversationId,
          },
        },
      );
      void refreshStream();
    } catch {
      setError("The agent could not queue that message. Please retry.");
    }
  }

  function resetPromptEditor() {
    setEditingPromptId(null);
    setPromptTitleDraft("");
    setPromptBodyDraft("");
    setPromptFavoriteDraft(false);
    setIsPromptEditorOpen(false);
  }

  function openCreatePromptEditor(prefillText = "") {
    setEditingPromptId(null);
    setPromptTitleDraft("");
    setPromptBodyDraft(prefillText);
    setPromptFavoriteDraft(false);
    setIsPromptEditorOpen(true);
  }

  function openEditPromptEditor(prompt) {
    setEditingPromptId(prompt.id);
    setPromptTitleDraft(prompt.title ?? "");
    setPromptBodyDraft(prompt.promptText ?? "");
    setPromptFavoriteDraft(prompt.isFavorite === true);
    setIsPromptEditorOpen(true);
  }

  async function requestPromptSuggestion({ mode, includePromptText, initialPrompt }) {
    setError("");
    setIsPromptGenerating(true);

    const result = await generateAgentPromptSuggestion({
      mode,
      includePromptText,
      initialPrompt,
      conversationMessages: conversationMessagesForGeneration,
    });

    setIsPromptGenerating(false);

    if (!result.success || !result.data) {
      setError(result.error || "Could not generate reusable prompt suggestion.");
      return null;
    }

    return result.data;
  }

  async function handleGenerateReusablePrompt() {
    if (isPromptGenerating || isPromptMutating) {
      return;
    }

    const initialPrompt = resolveInitialPromptCandidate();
    const suggestion = await requestPromptSuggestion({
      mode: "ConversationReusable",
      includePromptText: true,
      initialPrompt,
    });

    if (!suggestion) {
      return;
    }

    setEditingPromptId(null);
    setPromptTitleDraft(suggestion.title ?? "");
    setPromptBodyDraft(suggestion.promptText ?? initialPrompt);
    setPromptFavoriteDraft(false);
    setIsPromptEditorOpen(true);
    setIsPromptLibraryOpen(true);
  }

  async function handleGenerateFromInitialPrompt(includePromptText) {
    if (isPromptGenerating || isPromptMutating) {
      return;
    }

    const initialPrompt = resolveInitialPromptCandidate();
    if (!initialPrompt) {
      setError("Type a prompt first so AI can generate a title.");
      return;
    }

    const suggestion = await requestPromptSuggestion({
      mode: "InitialPrompt",
      includePromptText,
      initialPrompt,
    });

    if (!suggestion) {
      return;
    }

    setPromptTitleDraft(suggestion.title ?? "");
    if (includePromptText && typeof suggestion.promptText === "string" && suggestion.promptText.trim().length > 0) {
      setPromptBodyDraft(suggestion.promptText);
    }

    setIsPromptEditorOpen(true);
    setIsPromptLibraryOpen(true);
  }

  async function handlePromptSave(event) {
    event.preventDefault();
    if (isPromptMutating) {
      return;
    }

    const title = promptTitleDraft.trim();
    const promptText = promptBodyDraft.trim();
    if (!title || !promptText) {
      setError("Prompt title and content are required.");
      return;
    }

    setError("");
    setIsPromptMutating(true);
    const result = editingPromptId
      ? await updateAgentPrompt({
          id: editingPromptId,
          title,
          promptText,
          isFavorite: promptFavoriteDraft,
        })
      : await createAgentPrompt({
          title,
          promptText,
          isFavorite: promptFavoriteDraft,
        });
    setIsPromptMutating(false);

    if (!result.success) {
      setError(result.error || "Could not save prompt.");
      return;
    }

    resetPromptEditor();
    await refreshPromptLibrary(promptSearch);
  }

  async function handlePromptDelete(prompt) {
    if (!prompt?.id || prompt.scope !== "User" || isPromptMutating) {
      return;
    }

    if (typeof window !== "undefined" && !window.confirm(`Delete saved prompt \"${prompt.title}\"?`)) {
      return;
    }

    setError("");
    setIsPromptMutating(true);
    const result = await deleteAgentPrompt({ id: prompt.id });
    setIsPromptMutating(false);

    if (!result.success) {
      setError(result.error || "Could not delete prompt.");
      return;
    }

    if (editingPromptId === prompt.id) {
      resetPromptEditor();
    }

    await refreshPromptLibrary(promptSearch);
  }

  async function handlePromptFavoriteToggle(prompt) {
    if (!prompt?.id || prompt.scope !== "User" || isPromptMutating) {
      return;
    }

    setError("");
    setIsPromptMutating(true);
    const result = await updateAgentPrompt({
      id: prompt.id,
      isFavorite: !prompt.isFavorite,
    });
    setIsPromptMutating(false);

    if (!result.success) {
      setError(result.error || "Could not update prompt favorite.");
      return;
    }

    await refreshPromptLibrary(promptSearch);
  }

  async function handleSaveBaselinePrompt(prompt) {
    if (!prompt?.title || !prompt?.promptText || isPromptMutating) {
      return;
    }

    setError("");
    setIsPromptMutating(true);
    const result = await createAgentPrompt({
      title: prompt.title,
      promptText: prompt.promptText,
      isFavorite: false,
    });
    setIsPromptMutating(false);

    if (!result.success) {
      setError(result.error || "Could not save baseline prompt.");
      return;
    }

    await refreshPromptLibrary(promptSearch);
  }

  async function handlePromptExecute(prompt) {
    if (!prompt?.promptText || isSubmitting) {
      return;
    }

    setInputValue("");
    await queueMessage(prompt.promptText);

    if (prompt.scope === "User") {
      await recordAgentPromptUse({ id: prompt.id, conversationId });
      await refreshPromptLibrary(promptSearch);
    }
  }

  async function handleSendMessage(event) {
    event.preventDefault();

    const message = inputValue.trim();
    if (!message) {
      return;
    }

    setInputValue("");
    await queueMessage(message);
  }

  async function handleApprovalDecision(cardId, decision) {
    const card = approvalCards.find((entry) => entry.id === cardId);
    if (!card || card.status !== "pending") {
      return;
    }

    const confirmText = decision === "Approve"
      ? "Approve this high-impact agent action?"
      : "Reject this high-impact agent action?";

    if (typeof window !== "undefined" && !window.confirm(confirmText)) {
      return;
    }

    setError("");
    setApprovalCards((previous) =>
      previous.map((entry) =>
        entry.id === cardId ? { ...entry, status: "submitting" } : entry,
      ),
    );

    const result = await submitAgentApproval({
      conversationId,
      approvalId: card.commandId,
      decision,
      clientApprovalId: `${cardId}-${decision.toLowerCase()}`,
      rationale: decision === "Approve" ? "Approved by user in web agent panel." : "Rejected by user in web agent panel.",
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
            <p className="text-xs uppercase tracking-wider text-[var(--color-text-muted)]">Mosaic Copilot</p>
            <h2 className="text-base font-semibold text-[var(--color-text-main)]">Policy-aware runtime agent</h2>
          </div>
          <button
            type="button"
            onClick={() => setIsOpen(false)}
            className="rounded-md border border-[var(--color-border)] p-2 text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)] hover:text-[var(--color-text-main)]"
            aria-label="Close agent"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-3 grid grid-cols-2 gap-2">
          <div className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface-hover)] px-2.5 py-2">
            <p className="text-[10px] uppercase tracking-wide text-[var(--color-text-muted)]">Action Review</p>
            <p data-testid="agent-overview-pending" className="mt-0.5 text-xs font-semibold text-[var(--color-text-main)]">{pendingApprovals.length} pending</p>
          </div>
          <div className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface-hover)] px-2.5 py-2">
            <p className="text-[10px] uppercase tracking-wide text-[var(--color-text-muted)]">Latest Run</p>
            <p className="mt-0.5 text-xs font-semibold text-[var(--color-text-main)]">{latestRunStatus ?? "No runs yet"}</p>
          </div>
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
            Chat
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
            Runs
          </button>
        </div>
      </header>

      {error ? (
        <div className="mx-4 mt-3 rounded-lg border border-[var(--color-negative)]/30 bg-[var(--color-negative-bg)] px-3 py-2 text-xs text-[var(--color-negative)]">
          {error}
        </div>
      ) : null}

      {!error && chatError ? (
        <div className="mx-4 mt-3 rounded-lg border border-[var(--color-negative)]/30 bg-[var(--color-negative-bg)] px-3 py-2 text-xs text-[var(--color-negative)]">
          Something went wrong while streaming the agent response.
        </div>
      ) : null}

      {activeTab === "conversation" ? (
        <>
          {approvalCards.length > 0 ? (
            <section className="border-b border-[var(--color-border)] px-4 py-3">
              <div className="mb-2 flex items-center justify-between">
                <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Action Review</p>
                <span className="rounded-full border border-[var(--color-border)] px-2 py-0.5 text-[10px] text-[var(--color-text-muted)]">
                  <span data-testid="agent-action-review-pending">{pendingApprovals.length} pending</span>
                </span>
              </div>

              <div className="space-y-2 max-h-44 overflow-auto pr-1">
                {approvalCards.map((card) => (
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
                ))}
              </div>
            </section>
          ) : null}

          <section className="border-b border-[var(--color-border)] px-4 py-3">
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Reusable Prompts</p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => openCreatePromptEditor(inputValue.trim())}
                  className="inline-flex items-center gap-1 rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                >
                  <Plus className="h-3 w-3" />
                  Save Draft
                </button>
                <button
                  type="button"
                  onClick={() => void handleGenerateReusablePrompt()}
                  disabled={isPromptGenerating || isPromptMutating}
                  className="inline-flex items-center gap-1 rounded-md border border-[var(--color-primary)]/40 bg-[var(--color-primary)]/10 px-2 py-1 text-[10px] text-[var(--color-primary)] hover:bg-[var(--color-primary)]/20 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Sparkles className="h-3 w-3" />
                  {isPromptGenerating ? "Generating" : "Generate Reusable"}
                </button>
                <button
                  type="button"
                  onClick={() => setIsPromptLibraryOpen((current) => !current)}
                  className="inline-flex items-center gap-1 rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                >
                  <Bookmark className="h-3 w-3" />
                  {isPromptLibraryOpen ? "Hide" : "Browse"}
                </button>
              </div>
            </div>

            <div className="mt-2 flex flex-wrap gap-2">
              {promptLibrary.favorites.length > 0 ? (
                promptLibrary.favorites.slice(0, 3).map((prompt) => (
                  <button
                    key={prompt.id}
                    type="button"
                    onClick={() => void handlePromptExecute(prompt)}
                    disabled={isSubmitting || !conversationId}
                    className="rounded-full border border-[var(--color-primary)]/30 bg-[var(--color-primary)]/10 px-3 py-1 text-[11px] text-[var(--color-primary)] hover:bg-[var(--color-primary)]/20 disabled:cursor-not-allowed disabled:opacity-60"
                    title={prompt.promptText}
                  >
                    {prompt.title}
                  </button>
                ))
              ) : (
                <p className="text-[11px] text-[var(--color-text-muted)]">No favorites yet. Save and star prompts you reuse often.</p>
              )}
            </div>

            {isPromptLibraryOpen ? (
              <div className="mt-3 space-y-3 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-3">
                <label htmlFor="agent-prompt-search" className="sr-only">Search reusable prompts</label>
                <input
                  id="agent-prompt-search"
                  value={promptSearch}
                  onChange={(event) => setPromptSearch(event.target.value)}
                  placeholder="Search reusable prompts"
                  className="w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-xs text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] focus:border-[var(--color-primary)] focus:outline-none"
                />

                {isPromptLibraryLoading ? (
                  <p className="text-[11px] text-[var(--color-text-muted)]">Loading prompt library...</p>
                ) : (
                  <div className="space-y-3">
                    <div>
                      <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Saved</p>
                      <div className="space-y-2">
                        {promptLibrary.userPrompts.length === 0 ? (
                          <p className="text-[11px] text-[var(--color-text-muted)]">No saved prompts yet.</p>
                        ) : (
                          promptLibrary.userPrompts.map((prompt) => (
                            <article key={prompt.id} className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] p-2">
                              <p className="text-xs font-semibold text-[var(--color-text-main)]">{prompt.title}</p>
                              <p className="mt-0.5 line-clamp-2 text-[11px] text-[var(--color-text-muted)]">{prompt.promptText}</p>
                              <div className="mt-2 flex flex-wrap gap-1">
                                <button
                                  type="button"
                                  onClick={() => void handlePromptExecute(prompt)}
                                  disabled={isSubmitting || !conversationId}
                                  className="rounded-md bg-[var(--color-primary)] px-2 py-1 text-[10px] font-semibold text-[var(--color-button-ink)] hover:bg-[var(--color-primary-hover)] disabled:cursor-not-allowed disabled:opacity-60"
                                >
                                  Run
                                </button>
                                <button
                                  type="button"
                                  onClick={() => void handlePromptFavoriteToggle(prompt)}
                                  className="rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                                >
                                  {prompt.isFavorite ? <StarOff className="h-3 w-3" /> : <Star className="h-3 w-3" />}
                                </button>
                                <button
                                  type="button"
                                  onClick={() => openEditPromptEditor(prompt)}
                                  className="rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                                >
                                  <Pencil className="h-3 w-3" />
                                </button>
                                <button
                                  type="button"
                                  onClick={() => void handlePromptDelete(prompt)}
                                  className="rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-negative)] hover:bg-[var(--color-negative-bg)]"
                                >
                                  <Trash2 className="h-3 w-3" />
                                </button>
                              </div>
                            </article>
                          ))
                        )}
                      </div>
                    </div>

                    <div>
                      <p className="mb-1 text-[10px] font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">Baseline</p>
                      <div className="space-y-2">
                        {promptLibrary.baselinePrompts.length === 0 ? (
                          <p className="text-[11px] text-[var(--color-text-muted)]">No baseline prompts found.</p>
                        ) : (
                          promptLibrary.baselinePrompts.map((prompt) => (
                            <article key={prompt.id} className="rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] p-2">
                              <p className="text-xs font-semibold text-[var(--color-text-main)]">{prompt.title}</p>
                              <p className="mt-0.5 line-clamp-2 text-[11px] text-[var(--color-text-muted)]">{prompt.promptText}</p>
                              <div className="mt-2 flex flex-wrap gap-1">
                                <button
                                  type="button"
                                  onClick={() => void handlePromptExecute(prompt)}
                                  disabled={isSubmitting || !conversationId}
                                  className="rounded-md bg-[var(--color-primary)] px-2 py-1 text-[10px] font-semibold text-[var(--color-button-ink)] hover:bg-[var(--color-primary-hover)] disabled:cursor-not-allowed disabled:opacity-60"
                                >
                                  Run
                                </button>
                                <button
                                  type="button"
                                  onClick={() => void handleSaveBaselinePrompt(prompt)}
                                  className="rounded-md border border-[var(--color-border)] px-2 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                                >
                                  Save Copy
                                </button>
                              </div>
                            </article>
                          ))
                        )}
                      </div>
                    </div>
                  </div>
                )}

                {isPromptEditorOpen ? (
                  <form onSubmit={handlePromptSave} className="space-y-2 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] p-2">
                    <p className="text-[10px] font-semibold uppercase tracking-wide text-[var(--color-text-muted)]">
                      {editingPromptId ? "Edit saved prompt" : "Save reusable prompt"}
                    </p>
                    <input
                      value={promptTitleDraft}
                      onChange={(event) => setPromptTitleDraft(event.target.value)}
                      placeholder="Prompt title"
                      maxLength={120}
                      className="w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-xs text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] focus:border-[var(--color-primary)] focus:outline-none"
                    />
                    <textarea
                      value={promptBodyDraft}
                      onChange={(event) => setPromptBodyDraft(event.target.value)}
                      placeholder="Reusable prompt"
                      maxLength={1000}
                      rows={3}
                      className="w-full resize-none rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-xs text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] focus:border-[var(--color-primary)] focus:outline-none"
                    />
                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        type="button"
                        onClick={() => void handleGenerateFromInitialPrompt(false)}
                        disabled={isPromptGenerating || isPromptMutating}
                        className="rounded-md border border-[var(--color-border)] px-2.5 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)] disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {isPromptGenerating ? "Generating" : "AI Title"}
                      </button>
                      <button
                        type="button"
                        onClick={() => void handleGenerateFromInitialPrompt(true)}
                        disabled={isPromptGenerating || isPromptMutating}
                        className="rounded-md border border-[var(--color-primary)]/35 bg-[var(--color-primary)]/10 px-2.5 py-1 text-[10px] text-[var(--color-primary)] hover:bg-[var(--color-primary)]/20 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {isPromptGenerating ? "Generating" : "AI Polish + Title"}
                      </button>
                    </div>
                    <label className="flex items-center gap-2 text-[11px] text-[var(--color-text-main)]">
                      <input
                        type="checkbox"
                        checked={promptFavoriteDraft}
                        onChange={(event) => setPromptFavoriteDraft(event.target.checked)}
                      />
                      Pin as favorite
                    </label>
                    <div className="flex items-center gap-2">
                      <button
                        type="submit"
                        disabled={isPromptMutating}
                        className="rounded-md bg-[var(--color-primary)] px-2.5 py-1 text-[10px] font-semibold text-[var(--color-button-ink)] hover:bg-[var(--color-primary-hover)] disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {isPromptMutating ? "Saving" : "Save"}
                      </button>
                      <button
                        type="button"
                        onClick={resetPromptEditor}
                        className="rounded-md border border-[var(--color-border)] px-2.5 py-1 text-[10px] text-[var(--color-text-main)] hover:bg-[var(--color-surface-hover)]"
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                ) : null}
              </div>
            ) : null}
          </section>

          <section className="flex-1 overflow-auto px-4 py-3">
            <div className="space-y-3 pb-6">
              {messages.length === 0 ? (
                <div className="rounded-lg border border-dashed border-[var(--color-border)] p-4 text-xs text-[var(--color-text-muted)]">
                  <p className="font-semibold text-[var(--color-text-main)]">Start a guided conversation</p>
                  <p className="mt-1">Use your favorites or browse reusable prompts above, or type a fresh request below. High-impact requests still require approval.</p>
                </div>
              ) : (
                messages.map((message) => (
                  <article
                    key={message.id}
                    className={cn(
                      "rounded-xl border px-3 py-2 text-sm",
                      message.role === "user"
                        ? "ml-8 border-[var(--color-primary)]/25 bg-[var(--color-primary)]/10"
                        : "mr-8 border-[var(--color-border)] bg-[var(--color-surface-hover)]",
                    )}
                  >
                    <div className="mb-1 flex items-center gap-1 text-[10px] uppercase tracking-wide text-[var(--color-text-muted)]">
                      {message.role === "user" ? <User className="h-3 w-3" /> : <Bot className="h-3 w-3" />}
                      <span>{message.role}</span>
                      <span className="ml-auto normal-case tracking-normal">{formatTime(message.createdAt ?? message.updatedAt)}</span>
                    </div>
                    <p className="text-[var(--color-text-main)] whitespace-pre-wrap">{extractMessageText(message)}</p>
                  </article>
                ))
              )}

              {isSubmitting ? (
                <div 
                  className="mr-8 rounded-xl border border-[var(--color-primary)]/25 bg-[var(--color-primary)]/10 px-3 py-3 text-xs text-[var(--color-primary)] flex items-center"
                  aria-live="polite"
                  aria-atomic="true"
                >
                  <span className="sr-only">Mosaic is thinking...</span>
                  <div className="flex gap-1 items-center" aria-hidden="true">
                    <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-primary)] motion-safe:animate-bounce motion-reduce:animate-pulse [animation-delay:-0.3s]"></span>
                    <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-primary)] motion-safe:animate-bounce motion-reduce:animate-pulse [animation-delay:-0.15s]"></span>
                    <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-primary)] motion-safe:animate-bounce motion-reduce:animate-pulse"></span>
                  </div>
                </div>
              ) : null}
            </div>
          </section>

          <form onSubmit={handleSendMessage} className="border-t border-[var(--color-border)] px-4 py-3">
            <label htmlFor="agent-input" className="sr-only">
              Ask agent
            </label>
            <div className="flex items-end gap-2">
              <textarea
                id="agent-input"
                value={inputValue}
                onChange={(event) => setInputValue(event.target.value)}
                placeholder="Ask agent..."
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

              {(status === "submitted" || status === "streaming") ? (
                <button
                  type="button"
                  onClick={() => stop()}
                  className="inline-flex items-center gap-1 rounded-lg border border-[var(--color-border)] px-3 py-2 text-xs font-semibold text-[var(--color-text-muted)] hover:bg-[var(--color-surface-hover)]"
                >
                  Stop
                </button>
              ) : null}
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
                    {run.agentNoteSummary ? (
                      <div className="mt-1.5 rounded-md border border-[var(--color-primary)]/20 bg-[var(--color-primary)]/10 p-2">
                        <p className="text-[10px] font-semibold uppercase tracking-wide text-[var(--color-primary)]">Latest agent note</p>
                        <p className="mt-1 text-[11px] text-[var(--color-text-main)] break-words whitespace-pre-wrap">{run.agentNoteSummary}</p>
                      </div>
                    ) : null}
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
        className="fixed bottom-6 right-6 z-40 inline-flex items-center gap-2 rounded-full border border-[var(--color-border)] bg-[var(--color-surface)]/95 backdrop-blur-md px-5 py-2.5 text-sm font-semibold text-[var(--color-text-main)] shadow-2xl ring-1 ring-[var(--color-primary)]/20 hover:bg-[var(--color-surface-hover)] transition-all hover:scale-105 active:scale-95"
        aria-label={isOpen ? "Close agent" : "Open agent"}
      >
        {isOpen ? <PanelRightClose className="h-4 w-4 text-[var(--color-text-muted)]" /> : (
          <>
            <Sparkles className="h-4 w-4 text-[var(--color-primary)] animate-pulse" />
            <span className="bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-text-main)] bg-clip-text text-transparent">Agent</span>
          </>
        )}
      </button>

      <section
        aria-label="Global agent panel"
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
