import AsyncStorage from "@react-native-async-storage/async-storage";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AppState,
  type AppStateStatus,
  Pressable,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import type {
  AssistantApprovalDecision,
  AssistantCommandAcceptedDto,
  AssistantConversationRunStatusDto,
} from "../contracts";
import {
  getAssistantConversationStream,
  isRetriableAssistantError,
  postAssistantMessage,
  submitAssistantApproval,
  toReadableError,
} from "../services/mobileAgentApi";
import {
  enqueueAssistantPrompt,
  listAssistantPromptQueueEntries,
} from "../offline/assistantPromptQueue";
import { replayQueuedAssistantPrompts } from "../offline/assistantPromptRecovery";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";

const ASSISTANT_CONVERSATION_ID_STORAGE_KEY = "mosaic_money.mobile.assistant.conversation_id.v1";
const POLL_INTERVAL_MS = 7_000;

type AssistantTab = "conversation" | "timeline";
type AssistantMessageRole = "user" | "assistant" | "system";
type AssistantMessageTone = "normal" | "warning" | "error";
type AssistantApprovalCardStatus = "pending" | "submitting" | "approved" | "rejected";

interface LocalAssistantMessage {
  id: string;
  role: AssistantMessageRole;
  text: string;
  createdAt: string;
  tone: AssistantMessageTone;
}

interface AssistantApprovalCard {
  id: string;
  commandId: string;
  summary: string;
  createdAt: string;
  status: AssistantApprovalCardStatus;
}

function createClientId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function createConversationId(): string {
  return `conversation-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function formatTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "--";
  }

  return date.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function truncateSummary(value: string, limit: number = 120): string {
  if (value.length <= limit) {
    return value;
  }

  return `${value.slice(0, limit - 3)}...`;
}

function resolveRunStatusStyle(status: string): {
  backgroundColor: string;
  borderColor: string;
  color: string;
} {
  const normalized = status.toLowerCase();

  if (normalized === "completed") {
    return {
      backgroundColor: theme.colors.positiveBg,
      borderColor: theme.colors.positive,
      color: theme.colors.positive,
    };
  }

  if (normalized === "failed" || normalized === "needsreview") {
    return {
      backgroundColor: theme.colors.warningBg,
      borderColor: theme.colors.warning,
      color: theme.colors.warning,
    };
  }

  if (normalized === "running") {
    return {
      backgroundColor: "rgba(0, 240, 255, 0.12)",
      borderColor: theme.colors.primary,
      color: theme.colors.primary,
    };
  }

  return {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    color: theme.colors.textMuted,
  };
}

function buildAssistantQueueReplaySummary(result: {
  replayedCount: number;
  retriedCount: number;
  reconciledCount: number;
}): string {
  const parts: string[] = [];

  if (result.replayedCount > 0) {
    parts.push(`${result.replayedCount} replayed`);
  }

  if (result.retriedCount > 0) {
    parts.push(`${result.retriedCount} waiting retry`);
  }

  if (result.reconciledCount > 0) {
    parts.push(`${result.reconciledCount} dropped`);
  }

  if (parts.length === 0) {
    return "No queued assistant prompts were replayed.";
  }

  return `Replay summary: ${parts.join(", ")}.`;
}

export function AgentScreen() {
  const [conversationId, setConversationId] = useState<string>("");
  const [activeTab, setActiveTab] = useState<AssistantTab>("conversation");
  const [inputValue, setInputValue] = useState<string>("");
  const [messages, setMessages] = useState<LocalAssistantMessage[]>([]);
  const [approvalCards, setApprovalCards] = useState<AssistantApprovalCard[]>([]);
  const [timelineRuns, setTimelineRuns] = useState<AssistantConversationRunStatusDto[]>([]);
  const [queuedPromptCount, setQueuedPromptCount] = useState<number>(0);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [isRefreshingStream, setIsRefreshingStream] = useState<boolean>(false);
  const [isSyncingQueue, setIsSyncingQueue] = useState<boolean>(false);
  const [error, setError] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string>("");

  const appStateRef = useRef<AppStateStatus>(AppState.currentState);

  const pendingApprovals = useMemo(
    () => approvalCards.filter((card) => card.status === "pending"),
    [approvalCards],
  );

  const refreshQueueMetrics = useCallback(async () => {
    const queue = await listAssistantPromptQueueEntries();
    setQueuedPromptCount(queue.length);
  }, []);

  const refreshStream = useCallback(async () => {
    if (!conversationId) {
      return;
    }

    setIsRefreshingStream(true);

    try {
      const stream = await getAssistantConversationStream(conversationId);
      setTimelineRuns(stream.runs ?? []);
      setError("");
    } catch (streamError) {
      setError(toReadableError(streamError));
    } finally {
      setIsRefreshingStream(false);
    }
  }, [conversationId]);

  const applyAcceptedMessageState = useCallback((accepted: AssistantCommandAcceptedDto, prompt: string) => {
    const policyDisposition = accepted.policyDisposition ?? "advisory_only";

    setMessages((previous) => [
      ...previous,
      {
        id: `${accepted.commandId}-queued`,
        role: "assistant",
        text:
          policyDisposition === "approval_required"
            ? "This request is high-impact and now requires your explicit approval."
            : "Queued successfully. Run updates will appear in the provenance tab.",
        createdAt: accepted.queuedAtUtc ?? new Date().toISOString(),
        tone: policyDisposition === "approval_required" ? "warning" : "normal",
      },
    ]);

    if (policyDisposition === "approval_required") {
      setApprovalCards((previous) => [
        {
          id: accepted.commandId,
          commandId: accepted.commandId,
          summary: truncateSummary(prompt),
          createdAt: accepted.queuedAtUtc ?? new Date().toISOString(),
          status: "pending",
        },
        ...previous,
      ]);
    }
  }, []);

  useEffect(() => {
    let isCancelled = false;

    async function loadConversationId(): Promise<void> {
      try {
        const storedConversationId = await AsyncStorage.getItem(
          ASSISTANT_CONVERSATION_ID_STORAGE_KEY,
        );

        const nextConversationId = storedConversationId?.trim() || createConversationId();

        if (!storedConversationId?.trim()) {
          await AsyncStorage.setItem(
            ASSISTANT_CONVERSATION_ID_STORAGE_KEY,
            nextConversationId,
          );
        }

        if (!isCancelled) {
          setConversationId(nextConversationId);
        }
      } catch {
        if (!isCancelled) {
          setConversationId(createConversationId());
        }
      }
    }

    void loadConversationId();

    return () => {
      isCancelled = true;
    };
  }, []);

  useEffect(() => {
    void refreshQueueMetrics();
  }, [refreshQueueMetrics]);

  useEffect(() => {
    if (!conversationId) {
      return;
    }

    void refreshStream();

    const appStateSubscription = AppState.addEventListener("change", (nextState) => {
      const previousState = appStateRef.current;
      appStateRef.current = nextState;

      if (previousState !== "active" && nextState === "active") {
        void refreshStream();
      }
    });

    const timer = setInterval(() => {
      if (appStateRef.current === "active") {
        void refreshStream();
      }
    }, POLL_INTERVAL_MS);

    return () => {
      appStateSubscription.remove();
      clearInterval(timer);
    };
  }, [conversationId, refreshStream]);

  const handleReplayQueuedPrompts = useCallback(async () => {
    setIsSyncingQueue(true);

    try {
      const result = await replayQueuedAssistantPrompts();
      setStatusMessage(buildAssistantQueueReplaySummary(result));
      setError("");
      await refreshQueueMetrics();
      void refreshStream();
    } catch (replayError) {
      setError(toReadableError(replayError));
    } finally {
      setIsSyncingQueue(false);
    }
  }, [refreshQueueMetrics, refreshStream]);

  const handleSendMessage = useCallback(async () => {
    const prompt = inputValue.trim();
    if (!prompt || !conversationId || isSubmitting) {
      return;
    }

    const clientMessageId = createClientId("message");
    setIsSubmitting(true);
    setInputValue("");
    setError("");
    setStatusMessage("");

    setMessages((previous) => [
      ...previous,
      {
        id: clientMessageId,
        role: "user",
        text: prompt,
        createdAt: new Date().toISOString(),
        tone: "normal",
      },
    ]);

    try {
      const accepted = await postAssistantMessage(conversationId, {
        message: prompt,
        clientMessageId,
      });

      applyAcceptedMessageState(accepted, prompt);
      void refreshStream();
      await refreshQueueMetrics();
    } catch (sendError) {
      if (isRetriableAssistantError(sendError)) {
        await enqueueAssistantPrompt({
          conversationId,
          replayKey: `${conversationId}|${clientMessageId}`,
          summary: truncateSummary(prompt),
          request: {
            message: prompt,
            clientMessageId,
          },
        });

        setMessages((previous) => [
          ...previous,
          {
            id: `${clientMessageId}-queued`,
            role: "assistant",
            text: "Connection is unstable. Your prompt was queued offline and will replay automatically.",
            createdAt: new Date().toISOString(),
            tone: "warning",
          },
        ]);

        setStatusMessage("Prompt queued offline for replay.");
        await refreshQueueMetrics();
      } else {
        setError(toReadableError(sendError));
        setMessages((previous) => [
          ...previous,
          {
            id: `${clientMessageId}-error`,
            role: "system",
            text: "Assistant prompt failed. Please retry once connectivity stabilizes.",
            createdAt: new Date().toISOString(),
            tone: "error",
          },
        ]);
      }
    } finally {
      setIsSubmitting(false);
    }
  }, [
    applyAcceptedMessageState,
    conversationId,
    inputValue,
    isSubmitting,
    refreshQueueMetrics,
    refreshStream,
  ]);

  const handleApprovalDecision = useCallback(async (
    cardId: string,
    decision: AssistantApprovalDecision,
  ) => {
    const target = approvalCards.find((card) => card.id === cardId);
    if (!target || target.status !== "pending") {
      return;
    }

    setError("");
    setStatusMessage("");

    setApprovalCards((previous) =>
      previous.map((card) =>
        card.id === cardId
          ? { ...card, status: "submitting" }
          : card,
      ),
    );

    try {
      await submitAssistantApproval(conversationId, target.commandId, {
        decision,
        clientApprovalId: createClientId("approval"),
        rationale:
          decision === "Approve"
            ? "Approved by user from mobile assistant screen."
            : "Rejected by user from mobile assistant screen.",
      });

      setApprovalCards((previous) =>
        previous.map((card) =>
          card.id === cardId
            ? {
              ...card,
              status: decision === "Approve" ? "approved" : "rejected",
            }
            : card,
        ),
      );

      setMessages((previous) => [
        ...previous,
        {
          id: `${cardId}-${decision.toLowerCase()}`,
          role: "system",
          text: decision === "Approve" ? "Approval submitted." : "Rejection submitted.",
          createdAt: new Date().toISOString(),
          tone: "normal",
        },
      ]);

      void refreshStream();
    } catch (approvalError) {
      setError(toReadableError(approvalError));
      setApprovalCards((previous) =>
        previous.map((card) =>
          card.id === cardId ? { ...card, status: "pending" } : card,
        ),
      );
    }
  }, [approvalCards, conversationId, refreshStream]);

  return (
    <SafeAreaView style={styles.page}>
      <View style={styles.headerContainer}>
        <Text style={styles.heading}>Assistant</Text>
        <Text style={styles.subheading}>
          Policy-aware conversation lane with explicit approval controls.
        </Text>
        <PrimarySurfaceNav />

        <View style={styles.tabRow}>
          <Pressable
            accessibilityRole="button"
            onPress={() => setActiveTab("conversation")}
            style={({ pressed }) => [
              styles.tab,
              activeTab === "conversation" && styles.tabActive,
              pressed && styles.tabPressed,
            ]}
          >
            <Text style={[styles.tabText, activeTab === "conversation" && styles.tabTextActive]}>
              Conversation
            </Text>
          </Pressable>

          <Pressable
            accessibilityRole="button"
            onPress={() => setActiveTab("timeline")}
            style={({ pressed }) => [
              styles.tab,
              activeTab === "timeline" && styles.tabActive,
              pressed && styles.tabPressed,
            ]}
          >
            <Text style={[styles.tabText, activeTab === "timeline" && styles.tabTextActive]}>
              Provenance
            </Text>
          </Pressable>
        </View>

        <View style={styles.syncSummaryRow}>
          <Text style={styles.syncSummaryText}>Queued prompts: {queuedPromptCount}</Text>
          <Pressable
            accessibilityRole="button"
            disabled={isSyncingQueue || queuedPromptCount === 0}
            onPress={() => {
              void handleReplayQueuedPrompts();
            }}
            style={({ pressed }) => [
              styles.retryButton,
              (isSyncingQueue || queuedPromptCount === 0) && styles.retryButtonDisabled,
              pressed && !(isSyncingQueue || queuedPromptCount === 0) && styles.retryButtonPressed,
            ]}
          >
            <Text style={styles.retryButtonText}>
              {isSyncingQueue ? "Syncing..." : "Replay Queue"}
            </Text>
          </Pressable>
        </View>

        {statusMessage ? <Text style={styles.statusMessage}>{statusMessage}</Text> : null}
        {error ? <Text style={styles.errorMessage}>{error}</Text> : null}
      </View>

      {activeTab === "conversation" ? (
        <View style={styles.bodyContainer}>
          <View style={styles.approvalContainer}>
            <View style={styles.approvalHeaderRow}>
              <Text style={styles.approvalHeading}>Approval Queue</Text>
              <Text style={styles.approvalCount}>{pendingApprovals.length} pending</Text>
            </View>

            {approvalCards.length === 0 ? (
              <Text style={styles.emptyApprovalText}>No high-impact assistant actions are waiting for review.</Text>
            ) : (
              <ScrollView style={styles.approvalScroll} contentContainerStyle={styles.approvalScrollContent}>
                {approvalCards.map((card) => (
                  <View key={card.id} style={styles.approvalCard}>
                    <Text style={styles.approvalCardTitle}>High-impact action</Text>
                    <Text style={styles.approvalCardSummary}>{card.summary}</Text>
                    <Text style={styles.approvalCardMeta}>{formatTime(card.createdAt)}</Text>

                    <View style={styles.approvalFooter}>
                      <Text
                        style={[
                          styles.approvalStatus,
                          card.status === "approved"
                            ? styles.approvalStatusApproved
                            : card.status === "rejected"
                              ? styles.approvalStatusRejected
                              : card.status === "submitting"
                                ? styles.approvalStatusSubmitting
                                : styles.approvalStatusPending,
                        ]}
                      >
                        {card.status}
                      </Text>

                      {card.status === "pending" ? (
                        <View style={styles.approvalActionRow}>
                          <Pressable
                            accessibilityRole="button"
                            onPress={() => {
                              void handleApprovalDecision(card.id, "Approve");
                            }}
                            style={({ pressed }) => [
                              styles.approvalApproveButton,
                              pressed && styles.approvalApproveButtonPressed,
                            ]}
                          >
                            <Text style={styles.approvalApproveButtonText}>Approve</Text>
                          </Pressable>

                          <Pressable
                            accessibilityRole="button"
                            onPress={() => {
                              void handleApprovalDecision(card.id, "Reject");
                            }}
                            style={({ pressed }) => [
                              styles.approvalRejectButton,
                              pressed && styles.approvalRejectButtonPressed,
                            ]}
                          >
                            <Text style={styles.approvalRejectButtonText}>Reject</Text>
                          </Pressable>
                        </View>
                      ) : null}
                    </View>
                  </View>
                ))}
              </ScrollView>
            )}
          </View>

          <View style={styles.messagesContainer}>
            <ScrollView style={styles.messagesScroll} contentContainerStyle={styles.messagesScrollContent}>
              {messages.length === 0 ? (
                <View style={styles.emptyMessageCard}>
                  <Text style={styles.emptyMessageTitle}>Start a guided conversation</Text>
                  <Text style={styles.emptyMessageBody}>
                    Ask for help with categorization, review routing, or transaction context. High-impact requests will require approval.
                  </Text>
                </View>
              ) : (
                messages.map((message) => (
                  <View
                    key={message.id}
                    style={[
                      styles.messageCard,
                      message.role === "user"
                        ? styles.messageCardUser
                        : message.tone === "warning"
                          ? styles.messageCardWarning
                          : message.tone === "error"
                            ? styles.messageCardError
                            : styles.messageCardAssistant,
                    ]}
                  >
                    <View style={styles.messageMetaRow}>
                      <Text style={styles.messageRoleText}>{message.role}</Text>
                      <Text style={styles.messageTimeText}>{formatTime(message.createdAt)}</Text>
                    </View>
                    <Text style={styles.messageText}>{message.text}</Text>
                  </View>
                ))
              )}
            </ScrollView>

            <View style={styles.composeContainer}>
              <TextInput
                multiline
                editable={!isSubmitting}
                onChangeText={setInputValue}
                placeholder="Ask assistant..."
                placeholderTextColor={theme.colors.textSubtle}
                style={styles.composeInput}
                value={inputValue}
              />

              <Pressable
                accessibilityRole="button"
                disabled={isSubmitting || !conversationId || inputValue.trim().length === 0}
                onPress={() => {
                  void handleSendMessage();
                }}
                style={({ pressed }) => [
                  styles.sendButton,
                  (isSubmitting || !conversationId || inputValue.trim().length === 0) && styles.sendButtonDisabled,
                  pressed && !(isSubmitting || !conversationId || inputValue.trim().length === 0) && styles.sendButtonPressed,
                ]}
              >
                <Text style={styles.sendButtonText}>{isSubmitting ? "Sending..." : "Send"}</Text>
              </Pressable>
            </View>
          </View>
        </View>
      ) : (
        <View style={styles.timelineContainer}>
          <View style={styles.timelineHeader}>
            <Text style={styles.timelineHeading}>Run Timeline</Text>
            <Pressable
              accessibilityRole="button"
              onPress={() => {
                void refreshStream();
              }}
              style={({ pressed }) => [styles.timelineRefreshButton, pressed && styles.timelineRefreshButtonPressed]}
            >
              <Text style={styles.timelineRefreshButtonText}>
                {isRefreshingStream ? "Refreshing..." : "Refresh"}
              </Text>
            </Pressable>
          </View>

          <ScrollView style={styles.timelineScroll} contentContainerStyle={styles.timelineScrollContent}>
            {timelineRuns.length === 0 ? (
              <View style={styles.timelineEmptyCard}>
                <Text style={styles.timelineEmptyText}>No run provenance yet. Send a message to begin workflow tracking.</Text>
              </View>
            ) : (
              timelineRuns.map((run) => {
                const statusStyle = resolveRunStatusStyle(run.status);

                return (
                  <View key={run.runId} style={styles.timelineRunCard}>
                    <View style={styles.timelineRunHeader}>
                      <View style={styles.timelineRunMeta}>
                        <Text style={styles.timelineRunTitle}>{run.triggerSource}</Text>
                        <Text style={styles.timelineRunCorrelation}>{run.correlationId}</Text>
                      </View>
                      <View
                        style={[
                          styles.timelineStatusPill,
                          {
                            backgroundColor: statusStyle.backgroundColor,
                            borderColor: statusStyle.borderColor,
                          },
                        ]}
                      >
                        <Text style={[styles.timelineStatusText, { color: statusStyle.color }]}>{run.status}</Text>
                      </View>
                    </View>

                    <Text style={styles.timelineRunTime}>Created {formatTime(run.createdAtUtc)}</Text>
                    <Text style={styles.timelineRunTime}>Last update {formatTime(run.lastModifiedAtUtc)}</Text>

                    {run.failureCode ? (
                      <Text style={styles.timelineFailureText}>
                        {run.failureCode}
                        {run.failureRationale ? `: ${run.failureRationale}` : ""}
                      </Text>
                    ) : null}

                    {run.agentName || run.agentSource ? (
                      <Text style={styles.timelineAgentMetadata}>
                        Agent: {run.agentName ?? "Unknown"} ({run.agentSource ?? "Unknown Source"})
                      </Text>
                    ) : null}
                    
                    {run.latestStageOutcomeSummary ? (
                      <Text style={styles.timelineStageSummary}>
                        Outcome: {run.latestStageOutcomeSummary}
                      </Text>
                    ) : null}

                    {run.assignmentHint ? (
                      <Text style={styles.timelineAssignmentHint}>
                        Hint: {run.assignmentHint}
                      </Text>
                    ) : null}
                  </View>
                );
              })
            )}
          </ScrollView>
        </View>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  headerContainer: {
    backgroundColor: theme.colors.surface,
    borderBottomColor: theme.colors.border,
    borderBottomWidth: 1,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  heading: {
    color: theme.colors.textMain,
    fontSize: 28,
    fontWeight: "800",
    marginBottom: 4,
  },
  subheading: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginBottom: 8,
  },
  tabRow: {
    columnGap: 8,
    flexDirection: "row",
    marginTop: 12,
  },
  tab: {
    alignItems: "center",
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  tabActive: {
    borderColor: theme.colors.primary,
  },
  tabPressed: {
    opacity: 0.85,
  },
  tabText: {
    color: theme.colors.textMuted,
    fontSize: 12,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  tabTextActive: {
    color: theme.colors.primary,
  },
  syncSummaryRow: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 10,
  },
  syncSummaryText: {
    color: theme.colors.textMuted,
    fontSize: 13,
    fontWeight: "600",
  },
  retryButton: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  retryButtonDisabled: {
    opacity: 0.55,
  },
  retryButtonPressed: {
    opacity: 0.85,
  },
  retryButtonText: {
    color: theme.colors.primary,
    fontSize: 12,
    fontWeight: "700",
  },
  statusMessage: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 8,
  },
  errorMessage: {
    color: theme.colors.warning,
    fontSize: 12,
    marginTop: 6,
  },
  bodyContainer: {
    flex: 1,
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  approvalContainer: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    padding: 12,
  },
  approvalHeaderRow: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  approvalHeading: {
    color: theme.colors.textMain,
    fontSize: 13,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  approvalCount: {
    color: theme.colors.textMuted,
    fontSize: 11,
  },
  approvalScroll: {
    marginTop: 8,
    maxHeight: 150,
  },
  approvalScrollContent: {
    gap: 8,
    paddingBottom: 2,
  },
  approvalCard: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    padding: 10,
  },
  approvalCardTitle: {
    color: theme.colors.textMain,
    fontSize: 12,
    fontWeight: "700",
  },
  approvalCardSummary: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 4,
  },
  approvalCardMeta: {
    color: theme.colors.textSubtle,
    fontSize: 10,
    marginTop: 4,
  },
  approvalFooter: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 8,
  },
  approvalStatus: {
    borderRadius: theme.borderRadius.sm,
    borderWidth: 1,
    fontSize: 10,
    fontWeight: "700",
    overflow: "hidden",
    paddingHorizontal: 8,
    paddingVertical: 3,
    textTransform: "uppercase",
  },
  approvalStatusPending: {
    borderColor: theme.colors.warning,
    color: theme.colors.warning,
  },
  approvalStatusSubmitting: {
    borderColor: theme.colors.primary,
    color: theme.colors.primary,
  },
  approvalStatusApproved: {
    borderColor: theme.colors.positive,
    color: theme.colors.positive,
  },
  approvalStatusRejected: {
    borderColor: theme.colors.negative,
    color: theme.colors.negative,
  },
  approvalActionRow: {
    columnGap: 6,
    flexDirection: "row",
  },
  approvalApproveButton: {
    backgroundColor: theme.colors.approveBg,
    borderRadius: theme.borderRadius.sm,
    paddingHorizontal: 8,
    paddingVertical: 5,
  },
  approvalApproveButtonPressed: {
    opacity: 0.85,
  },
  approvalApproveButtonText: {
    color: theme.colors.approveText,
    fontSize: 11,
    fontWeight: "700",
  },
  approvalRejectButton: {
    borderColor: theme.colors.rejectBorder,
    borderRadius: theme.borderRadius.sm,
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 5,
  },
  approvalRejectButtonPressed: {
    opacity: 0.85,
  },
  approvalRejectButtonText: {
    color: theme.colors.rejectText,
    fontSize: 11,
    fontWeight: "700",
  },
  emptyApprovalText: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 8,
  },
  messagesContainer: {
    flex: 1,
    marginTop: 10,
  },
  messagesScroll: {
    flex: 1,
  },
  messagesScrollContent: {
    gap: 8,
    paddingBottom: 12,
  },
  emptyMessageCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderStyle: "dashed",
    borderWidth: 1,
    padding: 14,
  },
  emptyMessageTitle: {
    color: theme.colors.textMain,
    fontSize: 13,
    fontWeight: "700",
  },
  emptyMessageBody: {
    color: theme.colors.textMuted,
    fontSize: 12,
    lineHeight: 18,
    marginTop: 4,
  },
  messageCard: {
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 9,
  },
  messageCardUser: {
    alignSelf: "flex-end",
    backgroundColor: "rgba(0, 240, 255, 0.12)",
    borderColor: "rgba(0, 240, 255, 0.35)",
    maxWidth: "88%",
  },
  messageCardAssistant: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    maxWidth: "88%",
  },
  messageCardWarning: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.warningBg,
    borderColor: theme.colors.warning,
    maxWidth: "88%",
  },
  messageCardError: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.negativeBg,
    borderColor: theme.colors.negative,
    maxWidth: "88%",
  },
  messageMetaRow: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 4,
  },
  messageRoleText: {
    color: theme.colors.textSubtle,
    fontSize: 10,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  messageTimeText: {
    color: theme.colors.textSubtle,
    fontSize: 10,
  },
  messageText: {
    color: theme.colors.textMain,
    fontSize: 13,
    lineHeight: 18,
  },
  composeContainer: {
    columnGap: 8,
    flexDirection: "row",
    marginBottom: 8,
    marginTop: 8,
  },
  composeInput: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    color: theme.colors.textMain,
    flex: 1,
    fontSize: 14,
    maxHeight: 100,
    minHeight: 44,
    paddingHorizontal: 10,
    paddingVertical: 8,
    textAlignVertical: "top",
  },
  sendButton: {
    alignItems: "center",
    alignSelf: "flex-end",
    backgroundColor: theme.colors.primary,
    borderRadius: theme.borderRadius.md,
    justifyContent: "center",
    minHeight: 44,
    paddingHorizontal: 14,
  },
  sendButtonDisabled: {
    opacity: 0.5,
  },
  sendButtonPressed: {
    opacity: 0.85,
  },
  sendButtonText: {
    color: "#00131c",
    fontSize: 12,
    fontWeight: "800",
    textTransform: "uppercase",
  },
  timelineContainer: {
    flex: 1,
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  timelineHeader: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 10,
  },
  timelineHeading: {
    color: theme.colors.textMain,
    fontSize: 14,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  timelineRefreshButton: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    paddingHorizontal: 10,
    paddingVertical: 6,
  },
  timelineRefreshButtonPressed: {
    opacity: 0.85,
  },
  timelineRefreshButtonText: {
    color: theme.colors.primary,
    fontSize: 12,
    fontWeight: "700",
  },
  timelineScroll: {
    flex: 1,
  },
  timelineScrollContent: {
    gap: 8,
    paddingBottom: 24,
  },
  timelineEmptyCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderStyle: "dashed",
    borderWidth: 1,
    padding: 12,
  },
  timelineEmptyText: {
    color: theme.colors.textMuted,
    fontSize: 12,
    lineHeight: 18,
  },
  timelineRunCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    padding: 12,
  },
  timelineRunHeader: {
    alignItems: "flex-start",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  timelineRunMeta: {
    flex: 1,
    paddingRight: 10,
  },
  timelineRunTitle: {
    color: theme.colors.textMain,
    fontSize: 12,
    fontWeight: "700",
  },
  timelineRunCorrelation: {
    color: theme.colors.textSubtle,
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 10,
    marginTop: 4,
  },
  timelineStatusPill: {
    borderRadius: theme.borderRadius.sm,
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  timelineStatusText: {
    fontSize: 10,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  timelineRunTime: {
    color: theme.colors.textMuted,
    fontSize: 11,
    marginTop: 6,
  },
  timelineFailureText: {
    color: theme.colors.warning,
    fontSize: 11,
    marginTop: 6,
  },
  timelineAgentMetadata: {
    color: theme.colors.textSubtle,
    fontSize: 11,
    marginTop: 8,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  timelineStageSummary: {
    color: theme.colors.textMain,
    fontSize: 12,
    marginTop: 4,
    lineHeight: 18,
  },
  timelineAssignmentHint: {
    color: theme.colors.textMuted,
    fontSize: 11,
    fontStyle: "italic",
    marginTop: 4,
  },
});
