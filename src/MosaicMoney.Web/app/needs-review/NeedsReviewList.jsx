"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { reviewTransaction } from "./actions";
import { CurrencyDisplay } from "../../components/ui/CurrencyDisplay";

export default function NeedsReviewList({ transactions }) {
  const router = useRouter();
  const [loadingId, setLoadingId] = useState(null);
  const [error, setError] = useState(null);
  const [reclassifyId, setReclassifyId] = useState(null);
  const [rejectId, setRejectId] = useState(null);
  const [subcategoryId, setSubcategoryId] = useState("");
  const [needsReviewByUserId, setNeedsReviewByUserId] = useState("");
  const [reviewReason, setReviewReason] = useState("");

  if (!transactions || transactions.length === 0) {
    return (
      <div data-testid="needs-review-empty" className="bg-[var(--color-surface)] shadow rounded-xl border border-[var(--color-border)] p-6">
        <p className="text-[var(--color-text-muted)] text-center py-8">No items need review.</p>
      </div>
    );
  }

  const handleApprove = async (id) => {
    setLoadingId(id);
    setError(null);
    const result = await reviewTransaction(id, "approve");
    if (!result.success) {
      setError(result.error);
    } else {
      router.refresh();
    }
    setLoadingId(null);
  };

  const handleReclassify = async (id) => {
    if (!subcategoryId) {
      setError("Subcategory ID is required for reclassification.");
      return;
    }
    setLoadingId(id);
    setError(null);
    const result = await reviewTransaction(id, "reclassify", {
      subcategoryId,
      reviewReason: reviewReason || "Reclassified by user",
    });
    if (!result.success) {
      setError(result.error);
    } else {
      setReclassifyId(null);
      setSubcategoryId("");
      setReviewReason("");
      router.refresh();
    }
    setLoadingId(null);
  };

  const handleReject = async (id, currentNeedsReviewByUserId) => {
    if (!needsReviewByUserId && !currentNeedsReviewByUserId) {
      setError("NeedsReviewByUserId is required for reject action.");
      return;
    }

    setLoadingId(id);
    setError(null);
    const result = await reviewTransaction(id, "route_to_needs_review", {
      needsReviewByUserId: needsReviewByUserId || currentNeedsReviewByUserId,
      reviewReason: reviewReason || "Rejected by user",
    });

    if (!result.success) {
      setError(result.error);
    } else {
      setRejectId(null);
      setNeedsReviewByUserId("");
      setReviewReason("");
      router.refresh();
    }

    setLoadingId(null);
  };

  return (
    <div className="space-y-4">
      {error && (
        <div data-testid="needs-review-action-error" className="bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] px-4 py-3 rounded-md">
          {error}
        </div>
      )}
      {transactions.map((tx) => (
        <div key={tx.id} data-testid={`needs-review-item-${tx.id}`} className="bg-[var(--color-surface)] shadow rounded-xl border border-[var(--color-border)] p-6 hover:bg-[var(--color-surface-hover)] transition-colors">
          <div className="flex justify-between items-start">
            <div>
              <h3 className="text-lg font-medium text-white">{tx.description}</h3>
              <div className="flex items-center gap-2 mt-1">
                <p className="text-sm text-[var(--color-text-muted)]">
                  {new Date(tx.transactionDate).toLocaleDateString()}
                </p>
                <span className="text-[var(--color-text-muted)]">&middot;</span>
                <CurrencyDisplay amount={tx.amount} isTransfer={tx.excludeFromBudget} className="text-sm" />
              </div>
              {tx.reviewReason && (
                <p className="mt-2 text-sm text-[var(--color-warning)] bg-[var(--color-warning)]/10 p-2 rounded border border-[var(--color-warning)]/20">
                  <strong>Reason for review:</strong> {tx.reviewReason}
                </p>
              )}
              {(tx.userNote || tx.agentNote) && (
                <div className="mt-3 space-y-1">
                  {tx.userNote && (
                    <p className="text-sm text-[var(--color-text-muted)]">
                      <span className="font-medium text-white">User Note:</span> {tx.userNote}
                    </p>
                  )}
                  {tx.agentNote && (
                    <p className="text-sm text-[var(--color-text-muted)]">
                      <span className="font-medium text-white">Agent Note:</span> {tx.agentNote}
                    </p>
                  )}
                </div>
              )}
            </div>
            <div className="flex flex-col space-y-2 items-end">
              {reclassifyId === tx.id ? (
                <div className="flex flex-col space-y-2 items-end bg-[var(--color-surface-hover)] p-3 rounded-lg border border-[var(--color-border)]">
                  <input
                    data-testid={`needs-review-subcategory-${tx.id}`}
                    type="text"
                    placeholder="Subcategory ID (UUID)"
                    className="text-sm bg-[var(--color-background)] border border-[var(--color-border)] text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] rounded-md shadow-sm focus:ring-[var(--color-primary)] focus:border-[var(--color-primary)] px-3 py-2"
                    value={subcategoryId}
                    onChange={(e) => setSubcategoryId(e.target.value)}
                  />
                  <input
                    data-testid={`needs-review-reclassify-reason-${tx.id}`}
                    type="text"
                    placeholder="Reason (optional)"
                    className="text-sm bg-[var(--color-background)] border border-[var(--color-border)] text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] rounded-md shadow-sm focus:ring-[var(--color-primary)] focus:border-[var(--color-primary)] px-3 py-2"
                    value={reviewReason}
                    onChange={(e) => setReviewReason(e.target.value)}
                  />
                  <div className="flex space-x-2">
                    <button
                      data-testid={`needs-review-cancel-reclassify-${tx.id}`}
                      onClick={() => setReclassifyId(null)}
                      className="text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] transition-colors"
                      disabled={loadingId === tx.id}
                    >
                      Cancel
                    </button>
                    <button
                      data-testid={`needs-review-confirm-reclassify-${tx.id}`}
                      onClick={() => handleReclassify(tx.id)}
                      className="text-sm bg-[var(--color-primary)] text-[var(--color-text-main)] px-3 py-1.5 rounded-md hover:bg-[var(--color-primary-hover)] disabled:opacity-50 transition-colors font-semibold"
                      disabled={loadingId === tx.id}
                    >
                      {loadingId === tx.id ? "Saving..." : "Confirm"}
                    </button>
                  </div>
                </div>
              ) : rejectId === tx.id ? (
                <div className="flex flex-col space-y-2 items-end bg-[var(--color-surface-hover)] p-3 rounded-lg border border-[var(--color-border)]">
                  <input
                    data-testid={`needs-review-user-id-${tx.id}`}
                    type="text"
                    placeholder="NeedsReviewByUserId (UUID)"
                    className="text-sm bg-[var(--color-background)] border border-[var(--color-border)] text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] rounded-md shadow-sm focus:ring-[var(--color-primary)] focus:border-[var(--color-primary)] px-3 py-2"
                    value={needsReviewByUserId}
                    onChange={(e) => setNeedsReviewByUserId(e.target.value)}
                  />
                  <input
                    data-testid={`needs-review-reject-reason-${tx.id}`}
                    type="text"
                    placeholder="Reason (required)"
                    className="text-sm bg-[var(--color-background)] border border-[var(--color-border)] text-[var(--color-text-main)] placeholder:text-[var(--color-text-muted)] rounded-md shadow-sm focus:ring-[var(--color-primary)] focus:border-[var(--color-primary)] px-3 py-2"
                    value={reviewReason}
                    onChange={(e) => setReviewReason(e.target.value)}
                  />
                  <div className="flex space-x-2">
                    <button
                      data-testid={`needs-review-cancel-reject-${tx.id}`}
                      onClick={() => setRejectId(null)}
                      className="text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] transition-colors"
                      disabled={loadingId === tx.id}
                    >
                      Cancel
                    </button>
                    <button
                      data-testid={`needs-review-confirm-reject-${tx.id}`}
                      onClick={() => handleReject(tx.id, tx.needsReviewByUserId)}
                      className="text-sm bg-[var(--color-negative)] text-[var(--color-button-ink)] px-3 py-1.5 rounded-md hover:brightness-110 disabled:opacity-50 transition-colors font-semibold"
                      disabled={loadingId === tx.id}
                    >
                      {loadingId === tx.id ? "Saving..." : "Confirm Reject"}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex space-x-2">
                  <button
                    data-testid={`needs-review-reject-${tx.id}`}
                    onClick={() => {
                      setRejectId(tx.id);
                      setReviewReason(tx.reviewReason || "");
                    }}
                    className="text-sm bg-[var(--color-reject-bg)] border border-[var(--color-reject-border)] text-[var(--color-reject-text)] px-3 py-1.5 rounded-md hover:bg-[var(--color-reject-bg-hover)] disabled:opacity-50 transition-colors font-semibold"
                    disabled={loadingId === tx.id}
                  >
                    Reject
                  </button>
                  <button
                    data-testid={`needs-review-reclassify-${tx.id}`}
                    onClick={() => setReclassifyId(tx.id)}
                    className="text-sm bg-[var(--color-secondary-bg)] border border-[var(--color-secondary-border)] text-[var(--color-secondary-text)] px-3 py-1.5 rounded-md hover:bg-[var(--color-secondary-bg-hover)] disabled:opacity-50 transition-colors font-medium"
                    disabled={loadingId === tx.id}
                  >
                    Reclassify
                  </button>
                  <button
                    data-testid={`needs-review-approve-${tx.id}`}
                    onClick={() => handleApprove(tx.id)}
                    className="text-sm bg-[var(--color-approve-bg)] text-[var(--color-approve-text)] px-3 py-1.5 rounded-md hover:bg-[var(--color-approve-bg-hover)] disabled:opacity-50 transition-colors font-semibold"
                    disabled={loadingId === tx.id}
                  >
                    {loadingId === tx.id ? "Approving..." : "Approve"}
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
