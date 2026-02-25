import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert, AppState } from "react-native";
import type { ReviewActionRequest, TransactionDto } from "../contracts";
import {
  clearReviewMutationBackoff,
  enqueueReviewMutation,
  getPendingReviewMutationForTransaction,
  mapReviewQueueEntryForDisplay,
  markReviewMutationReplayFailure,
  removeReviewMutation,
  removeReviewMutationForTransaction,
} from "../offline/reviewMutationQueue";
import {
  clearReviewMutationReconciliationNoticeForTransaction,
  getReviewMutationErrorCode,
  getReviewMutationReconciliationDetails,
  getReviewMutationReconciliationNoticeForTransaction,
  isRetriableReviewMutationError,
  recordReviewMutationReconciliationNotice,
} from "../offline/reviewMutationRecovery";
import { submitReviewAction } from "../services/mobileTransactionsApi";
import {
  toReadableError as toReadableApiError,
} from "../../../shared/services/mobileApiClient";

type ReviewActionKind = "approve" | "reject";

interface PendingSyncReviewAction {
  queueEntryId: string;
  kind: ReviewActionKind;
  request: ReviewActionRequest;
  queuedAtUtc: string;
  attemptCount: number;
  nextAttemptAtUtc?: string;
}

interface UseTransactionReviewActionsOptions {
  transaction: TransactionDto;
  onActionSynced?: () => Promise<void>;
}

interface UseTransactionReviewActionsState {
  rejectReason: string;
  setRejectReason: (nextReason: string) => void;
  isSubmitting: boolean;
  canReject: boolean;
  rejectDisabledReason?: string;
  statusMessage: string | null;
  errorMessage: string | null;
  pendingSyncAction: PendingSyncReviewAction | null;
  approve: (subcategoryId?: string) => Promise<void>;
  reject: () => Promise<void>;
  retryPendingSync: () => Promise<void>;
}

function getDefaultRejectReason(transaction: TransactionDto): string {
  const existingReason = transaction.reviewReason?.trim();
  return existingReason && existingReason.length > 0
    ? existingReason
    : "Rejected by mobile reviewer.";
}

function requestConfirmation(options: {
  title: string;
  message: string;
  confirmLabel: string;
  destructive?: boolean;
}): Promise<boolean> {
  return new Promise((resolve) => {
    let resolved = false;
    const finish = (value: boolean) => {
      if (resolved) {
        return;
      }

      resolved = true;
      resolve(value);
    };

    Alert.alert(
      options.title,
      options.message,
      [
        {
          text: "Cancel",
          style: "cancel",
          onPress: () => finish(false),
        },
        {
          text: options.confirmLabel,
          style: options.destructive ? "destructive" : "default",
          onPress: () => finish(true),
        },
      ],
      {
        cancelable: true,
        onDismiss: () => finish(false),
      },
    );
  });
}

export function useTransactionReviewActions({
  transaction,
  onActionSynced,
}: UseTransactionReviewActionsOptions): UseTransactionReviewActionsState {
  const [rejectReason, setRejectReason] = useState(() =>
    getDefaultRejectReason(transaction),
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [pendingSyncAction, setPendingSyncAction] =
    useState<PendingSyncReviewAction | null>(null);

  useEffect(() => {
    setRejectReason(getDefaultRejectReason(transaction));
    setStatusMessage(null);
    setErrorMessage(null);
    setPendingSyncAction(null);

    let isActive = true;
    const loadPendingSyncState = async () => {
      const [pendingEntry, reconciliationNotice] = await Promise.all([
        getPendingReviewMutationForTransaction(transaction.id),
        getReviewMutationReconciliationNoticeForTransaction(transaction.id),
      ]);

      if (!isActive) {
        return;
      }

      if (pendingEntry) {
        setPendingSyncAction(mapReviewQueueEntryForDisplay(pendingEntry));
        setStatusMessage(
          "Action confirmation was captured on this device and is waiting to sync with Mosaic Money backend.",
        );
        return;
      }

      setPendingSyncAction(null);

      if (reconciliationNotice) {
        setStatusMessage(reconciliationNotice.message);
      }
    };

    void loadPendingSyncState();

    const appStateSubscription = AppState.addEventListener("change", (nextState) => {
      if (nextState === "active") {
        void loadPendingSyncState();
      }
    });

    return () => {
      isActive = false;
      appStateSubscription.remove();
    };
  }, [transaction.id, transaction.reviewReason]);

  const canReject = useMemo(
    () => Boolean(transaction.needsReviewByUserId),
    [transaction.needsReviewByUserId],
  );

  const rejectDisabledReason = useMemo(() => {
    if (canReject) {
      return undefined;
    }

    return "Reject requires an assigned NeedsReviewByUserId on this transaction.";
  }, [canReject]);

  const submitAndSync = useCallback(
    async (kind: ReviewActionKind, request: ReviewActionRequest) => {
      setIsSubmitting(true);
      setErrorMessage(null);
      setStatusMessage(null);

      try {
        await submitReviewAction(request);
        await removeReviewMutationForTransaction(request.transactionId);
        await clearReviewMutationReconciliationNoticeForTransaction(
          request.transactionId,
        );
        setPendingSyncAction(null);
        setStatusMessage(
          kind === "approve"
            ? "Transaction approved and synced with backend."
            : "Reject action synced with backend.",
        );

        if (onActionSynced) {
          await onActionSynced();
        }
      } catch (error) {
        const readable = toReadableApiError(
          error,
          "Unexpected error while sending review action.",
        );
        setErrorMessage(readable);

        if (isRetriableReviewMutationError(error)) {
          try {
            const queuedEntry = await enqueueReviewMutation({
              actionKind: kind,
              request,
            });

            await clearReviewMutationReconciliationNoticeForTransaction(
              request.transactionId,
            );
            setPendingSyncAction(mapReviewQueueEntryForDisplay(queuedEntry));
            setStatusMessage(
              "Action confirmation captured but sync is pending. Retry when connectivity is restored.",
            );
          } catch (queueError) {
            setPendingSyncAction(null);
            setErrorMessage(
              toReadableApiError(
                queueError,
                "Unable to persist pending review action for offline retry.",
              ),
            );
          }
        } else {
          setPendingSyncAction(null);
        }
      } finally {
        setIsSubmitting(false);
      }
    },
    [onActionSynced],
  );

  const approve = useCallback(async (subcategoryId?: string) => {
    const confirmed = await requestConfirmation({
      title: "Approve transaction?",
      message:
        "This will send an approval action to Mosaic Money backend. Continue?",
      confirmLabel: "Approve",
    });

    if (!confirmed) {
      return;
    }

    await submitAndSync("approve", {
      transactionId: transaction.id,
      action: "approve",
      subcategoryId,
    });
  }, [submitAndSync, transaction.id]);

  const reject = useCallback(async () => {
    const normalizedReason = rejectReason.trim();

    if (!canReject || !transaction.needsReviewByUserId) {
      setErrorMessage(
        rejectDisabledReason ??
          "Reject action is unavailable for this transaction.",
      );
      return;
    }

    if (!normalizedReason) {
      setErrorMessage("Provide a rejection reason before confirming reject.");
      return;
    }

    const confirmed = await requestConfirmation({
      title: "Reject transaction?",
      message:
        "Reject keeps this transaction in NeedsReview and records your explicit human decision. Continue?",
      confirmLabel: "Reject",
      destructive: true,
    });

    if (!confirmed) {
      return;
    }

    await submitAndSync("reject", {
      transactionId: transaction.id,
      action: "route_to_needs_review",
      reviewReason: normalizedReason,
      needsReviewByUserId: transaction.needsReviewByUserId,
    });
  }, [
    canReject,
    rejectDisabledReason,
    rejectReason,
    submitAndSync,
    transaction.id,
    transaction.needsReviewByUserId,
  ]);

  const retryPendingSync = useCallback(async () => {
    if (!pendingSyncAction || isSubmitting) {
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);
    setStatusMessage(null);

    try {
      await clearReviewMutationBackoff(pendingSyncAction.queueEntryId);
      await submitReviewAction(pendingSyncAction.request);
      await removeReviewMutation(pendingSyncAction.queueEntryId);
      await clearReviewMutationReconciliationNoticeForTransaction(
        pendingSyncAction.request.transactionId,
      );
      setPendingSyncAction(null);
      setStatusMessage("Pending action synced with backend.");

      if (onActionSynced) {
        await onActionSynced();
      }
    } catch (error) {
      const readable = toReadableApiError(
        error,
        "Unexpected error while retrying pending sync.",
      );
      setErrorMessage(readable);

      if (!isRetriableReviewMutationError(error)) {
        const reconciliationDetails =
          getReviewMutationReconciliationDetails(error);

        await removeReviewMutation(pendingSyncAction.queueEntryId);

        const reconciliationNotice = await recordReviewMutationReconciliationNotice(
          {
            queueEntryId: pendingSyncAction.queueEntryId,
            transactionId: pendingSyncAction.request.transactionId,
            reason: reconciliationDetails?.reason ?? "non_retriable",
            errorCode:
              reconciliationDetails?.errorCode ??
              getReviewMutationErrorCode(error),
            message:
              reconciliationDetails?.message ??
              "Queued review action was reconciled and removed.",
          },
        );

        setPendingSyncAction(null);
        setStatusMessage(reconciliationNotice.message);
        return;
      }

      const updatedEntry = await markReviewMutationReplayFailure({
        entryId: pendingSyncAction.queueEntryId,
        errorCode: getReviewMutationErrorCode(error),
      });

      if (!updatedEntry) {
        setPendingSyncAction(null);
        setStatusMessage("Pending sync state changed. Refresh to check current status.");
        return;
      }

      setPendingSyncAction(mapReviewQueueEntryForDisplay(updatedEntry));
      setStatusMessage(
        "Retry failed and action remains queued. Use Retry Sync again when connectivity is stable.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }, [isSubmitting, onActionSynced, pendingSyncAction]);

  return {
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
  };
}
