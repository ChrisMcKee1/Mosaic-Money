import { Pressable, StyleSheet, Text, TextInput, View } from "react-native";
import type { TransactionDto } from "../contracts";
import { useTransactionReviewActions } from "../hooks/useTransactionReviewActions";
import { formatUtcDateTime } from "../utils/formatters";

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
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    padding: 16,
  },
  sectionTitle: {
    color: "#101828",
    fontSize: 18,
    fontWeight: "700",
  },
  sectionBody: {
    color: "#475467",
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
    backgroundColor: "#175cd3",
    borderRadius: 10,
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  primaryButtonPressed: {
    opacity: 0.9,
  },
  primaryButtonText: {
    color: "#ffffff",
    fontSize: 14,
    fontWeight: "700",
  },
  rejectButton: {
    alignItems: "center",
    backgroundColor: "#b42318",
    borderRadius: 10,
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  rejectButtonPressed: {
    opacity: 0.9,
  },
  rejectButtonText: {
    color: "#ffffff",
    fontSize: 14,
    fontWeight: "700",
  },
  buttonDisabled: {
    opacity: 0.55,
  },
  inputLabel: {
    color: "#475467",
    fontSize: 12,
    fontWeight: "700",
    marginTop: 14,
    textTransform: "uppercase",
  },
  reasonInput: {
    backgroundColor: "#fcfdff",
    borderColor: "#d0d8e3",
    borderRadius: 10,
    borderWidth: 1,
    color: "#101828",
    fontSize: 14,
    marginTop: 8,
    minHeight: 92,
    padding: 12,
    textAlignVertical: "top",
  },
  helperText: {
    color: "#475467",
    fontSize: 12,
    marginTop: 8,
  },
  noticeInfo: {
    backgroundColor: "#eef4ff",
    borderColor: "#bfd3ff",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
  },
  noticeInfoText: {
    color: "#1849a9",
    fontSize: 13,
    lineHeight: 18,
  },
  pendingSyncCard: {
    backgroundColor: "#fffaeb",
    borderColor: "#fedf89",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
    rowGap: 8,
  },
  pendingSyncTitle: {
    color: "#7a2e0e",
    fontSize: 13,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  pendingSyncBody: {
    color: "#7a2e0e",
    fontSize: 13,
    lineHeight: 18,
  },
  pendingSyncMeta: {
    color: "#9a3412",
    fontSize: 12,
    lineHeight: 16,
  },
  retryButton: {
    alignItems: "center",
    alignSelf: "flex-start",
    backgroundColor: "#f79009",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  retryButtonPressed: {
    opacity: 0.9,
  },
  retryButtonText: {
    color: "#ffffff",
    fontSize: 13,
    fontWeight: "700",
  },
  noticeError: {
    backgroundColor: "#fef3f2",
    borderColor: "#fecdca",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 12,
    padding: 10,
  },
  noticeErrorText: {
    color: "#b42318",
    fontSize: 13,
    lineHeight: 18,
  },
});
