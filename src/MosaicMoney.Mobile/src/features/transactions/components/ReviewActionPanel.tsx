import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import type { TransactionDto } from "../contracts";
import { useTransactionReviewActions } from "../hooks/useTransactionReviewActions";
import { formatUtcDateTime } from "../utils/formatters";
import { theme } from "../../../theme/tokens";

interface ReviewActionPanelProps {
  transaction: TransactionDto;
  onActionSynced?: () => Promise<void>;
}

export function ReviewActionPanel({
  transaction,
  onActionSynced,
}: ReviewActionPanelProps) {
  const {
    rejectReason,
    setRejectReason,
    isSubmitting,
    canReject,
    rejectDisabledReason,
    statusMessage,
    errorMessage,
    pendingSyncAction,
    approve,
    reject,
    retryPendingSync,
  } = useTransactionReviewActions({ transaction, onActionSynced });

  const reviewPending = transaction.reviewStatus === "NeedsReview";

  return (
    <View style={styles.sectionCard}>
      <Text style={styles.sectionTitle}>Human Review Actions</Text>
      <Text style={styles.sectionBody}>
        Approval and reject actions require explicit user confirmation and are
        committed through backend review endpoints only.
      </Text>

      {!reviewPending ? (
        <View style={styles.noticeInfo}>
          <Text style={styles.noticeInfoText}>
            Review actions are unavailable because this transaction is no longer
            in NeedsReview.
          </Text>
        </View>
      ) : null}

      {reviewPending ? (
        <>
          <View style={styles.actionsRow}>
            <Pressable
              accessibilityRole="button"
              onPress={() => {
                void approve();
              }}
              disabled={isSubmitting}
              style={({ pressed }) => [
                styles.primaryButton,
                (pressed || isSubmitting) && styles.primaryButtonPressed,
                isSubmitting && styles.buttonDisabled,
              ]}
            >
              <Text style={styles.primaryButtonText}>
                {isSubmitting ? "Submitting..." : "Approve"}
              </Text>
            </Pressable>

            <Pressable
              accessibilityRole="button"
              onPress={() => {
                void reject();
              }}
              disabled={isSubmitting || !canReject || rejectReason.trim().length === 0}
              style={({ pressed }) => [
                styles.rejectButton,
                (pressed || isSubmitting) && styles.rejectButtonPressed,
                (isSubmitting || !canReject || rejectReason.trim().length === 0) &&
                  styles.buttonDisabled,
              ]}
            >
              <Text style={styles.rejectButtonText}>Reject</Text>
            </Pressable>
          </View>

          <Text style={styles.inputLabel}>Reject reason</Text>
          <TextInput
            editable={!isSubmitting && canReject}
            multiline
            numberOfLines={3}
            placeholder="Document why this should remain in NeedsReview."
            style={styles.reasonInput}
            value={rejectReason}
            onChangeText={setRejectReason}
          />

          {rejectDisabledReason ? (
            <Text style={styles.helperText}>{rejectDisabledReason}</Text>
          ) : null}
        </>
      ) : null}

      {statusMessage ? (
        <View style={styles.noticeInfo}>
          <Text style={styles.noticeInfoText}>{statusMessage}</Text>
        </View>
      ) : null}

      {pendingSyncAction ? (
        <View style={styles.pendingSyncCard}>
          <Text style={styles.pendingSyncTitle}>Pending sync</Text>
          <Text style={styles.pendingSyncBody}>
            {pendingSyncAction.kind === "approve" ? "Approve" : "Reject"} was
            confirmed on device at {formatUtcDateTime(pendingSyncAction.queuedAtUtc)}
            but did not sync yet.
          </Text>
          <Text style={styles.pendingSyncMeta}>
            Retry attempts: {pendingSyncAction.attemptCount}
            {pendingSyncAction.nextAttemptAtUtc
              ? ` - Backoff until ${formatUtcDateTime(pendingSyncAction.nextAttemptAtUtc)}`
              : ""}
          </Text>
          <Pressable
            accessibilityRole="button"
            onPress={() => {
              void retryPendingSync();
            }}
            disabled={isSubmitting}
            style={({ pressed }) => [
              styles.retryButton,
              (pressed || isSubmitting) && styles.retryButtonPressed,
              isSubmitting && styles.buttonDisabled,
            ]}
          >
            <Text style={styles.retryButtonText}>
              {isSubmitting ? "Retrying..." : "Retry Sync"}
            </Text>
          </Pressable>
        </View>
      ) : null}

      {errorMessage ? (
        <View style={styles.noticeError}>
          <Text style={styles.noticeErrorText}>{errorMessage}</Text>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  sectionCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 12,
    borderWidth: 1,
    padding: 16,
  },
  sectionTitle: {
    color: theme.colors.textMain,
    fontSize: 18,
    fontWeight: "700",
  },
  sectionBody: {
    color: theme.colors.textMuted,
    fontSize: 13,
    lineHeight: 18,
    marginTop: 8,
  },
  actionsRow: {
    columnGap: 10,
    flexDirection: "row",
    marginTop: 14,
  },
  primaryButton: {
    alignItems: "center",
    backgroundColor: theme.colors.approveBg,
    borderRadius: 10,
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  primaryButtonPressed: {
    backgroundColor: theme.colors.positive,
  },
  primaryButtonText: {
    color: theme.colors.approveText,
    fontSize: 14,
    fontWeight: "700",
  },
  rejectButton: {
    alignItems: "center",
    backgroundColor: theme.colors.rejectBg,
    borderColor: theme.colors.rejectBorder,
    borderWidth: 1,
    borderRadius: 10,
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  rejectButtonPressed: {
    backgroundColor: theme.colors.negativeBg,
  },
  rejectButtonText: {
    color: theme.colors.rejectText,
    fontSize: 14,
    fontWeight: "700",
  },
  buttonDisabled: {
    opacity: 0.55,
  },
  inputLabel: {
    color: theme.colors.textMuted,
    fontSize: 12,
    fontWeight: "700",
    marginTop: 14,
    textTransform: "uppercase",
  },
  reasonInput: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: 10,
    borderWidth: 1,
    color: theme.colors.textMain,
    fontSize: 14,
    marginTop: 8,
    minHeight: 92,
    padding: 12,
    textAlignVertical: "top",
  },
  helperText: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 8,
  },
  noticeInfo: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.primary,
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
  },
  noticeInfoText: {
    color: theme.colors.primary,
    fontSize: 13,
    lineHeight: 18,
  },
  pendingSyncCard: {
    backgroundColor: theme.colors.warningBg,
    borderColor: theme.colors.warning,
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
    rowGap: 8,
  },
  pendingSyncTitle: {
    color: theme.colors.warning,
    fontSize: 13,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  pendingSyncBody: {
    color: theme.colors.warning,
    fontSize: 13,
    lineHeight: 18,
  },
  pendingSyncMeta: {
    color: theme.colors.warning,
    fontSize: 12,
    lineHeight: 16,
  },
  retryButton: {
    alignItems: "center",
    alignSelf: "flex-start",
    backgroundColor: theme.colors.warning,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  retryButtonPressed: {
    opacity: 0.9,
  },
  retryButtonText: {
    color: theme.colors.background,
    fontSize: 13,
    fontWeight: "700",
  },
  noticeError: {
    backgroundColor: theme.colors.negativeBg,
    borderColor: theme.colors.negative,
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
  },
  noticeErrorText: {
    color: theme.colors.negative,
    fontSize: 13,
    lineHeight: 18,
  },
});
