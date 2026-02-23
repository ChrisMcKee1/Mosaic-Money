"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { reviewTransaction } from "./actions";

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
      <div data-testid="needs-review-empty" className="bg-white shadow rounded-lg border border-gray-200 p-6">
        <p className="text-gray-500 text-center py-8">No items need review.</p>
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
        <div data-testid="needs-review-action-error" className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {error}
        </div>
      )}
      {transactions.map((tx) => (
        <div key={tx.id} data-testid={`needs-review-item-${tx.id}`} className="bg-white shadow rounded-lg border border-gray-200 p-6">
          <div className="flex justify-between items-start">
            <div>
              <h3 className="text-lg font-medium text-gray-900">{tx.description}</h3>
              <p className="text-sm text-gray-500">
                {new Date(tx.transactionDate).toLocaleDateString()} &middot; ${tx.amount.toFixed(2)}
              </p>
              {tx.reviewReason && (
                <p className="mt-2 text-sm text-amber-700 bg-amber-50 p-2 rounded border border-amber-100">
                  <strong>Reason for review:</strong> {tx.reviewReason}
                </p>
              )}
              {(tx.userNote || tx.agentNote) && (
                <div className="mt-3 space-y-1">
                  {tx.userNote && (
                    <p className="text-sm text-gray-600">
                      <span className="font-medium">User Note:</span> {tx.userNote}
                    </p>
                  )}
                  {tx.agentNote && (
                    <p className="text-sm text-gray-600">
                      <span className="font-medium">Agent Note:</span> {tx.agentNote}
                    </p>
                  )}
                </div>
              )}
            </div>
            <div className="flex flex-col space-y-2 items-end">
              {reclassifyId === tx.id ? (
                <div className="flex flex-col space-y-2 items-end bg-gray-50 p-3 rounded border border-gray-200">
                  <input
                    data-testid={`needs-review-subcategory-${tx.id}`}
                    type="text"
                    placeholder="Subcategory ID (UUID)"
                    className="text-sm border-gray-300 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500"
                    value={subcategoryId}
                    onChange={(e) => setSubcategoryId(e.target.value)}
                  />
                  <input
                    data-testid={`needs-review-reclassify-reason-${tx.id}`}
                    type="text"
                    placeholder="Reason (optional)"
                    className="text-sm border-gray-300 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500"
                    value={reviewReason}
                    onChange={(e) => setReviewReason(e.target.value)}
                  />
                  <div className="flex space-x-2">
                    <button
                      data-testid={`needs-review-cancel-reclassify-${tx.id}`}
                      onClick={() => setReclassifyId(null)}
                      className="text-sm text-gray-500 hover:text-gray-700"
                      disabled={loadingId === tx.id}
                    >
                      Cancel
                    </button>
                    <button
                      data-testid={`needs-review-confirm-reclassify-${tx.id}`}
                      onClick={() => handleReclassify(tx.id)}
                      className="text-sm bg-blue-600 text-white px-3 py-1 rounded hover:bg-blue-700 disabled:opacity-50"
                      disabled={loadingId === tx.id}
                    >
                      {loadingId === tx.id ? "Saving..." : "Confirm"}
                    </button>
                  </div>
                </div>
              ) : rejectId === tx.id ? (
                <div className="flex flex-col space-y-2 items-end bg-gray-50 p-3 rounded border border-gray-200">
                  <input
                    data-testid={`needs-review-user-id-${tx.id}`}
                    type="text"
                    placeholder="NeedsReviewByUserId (UUID)"
                    className="text-sm border-gray-300 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500"
                    value={needsReviewByUserId}
                    onChange={(e) => setNeedsReviewByUserId(e.target.value)}
                  />
                  <input
                    data-testid={`needs-review-reject-reason-${tx.id}`}
                    type="text"
                    placeholder="Reason (required)"
                    className="text-sm border-gray-300 rounded-md shadow-sm focus:ring-blue-500 focus:border-blue-500"
                    value={reviewReason}
                    onChange={(e) => setReviewReason(e.target.value)}
                  />
                  <div className="flex space-x-2">
                    <button
                      data-testid={`needs-review-cancel-reject-${tx.id}`}
                      onClick={() => setRejectId(null)}
                      className="text-sm text-gray-500 hover:text-gray-700"
                      disabled={loadingId === tx.id}
                    >
                      Cancel
                    </button>
                    <button
                      data-testid={`needs-review-confirm-reject-${tx.id}`}
                      onClick={() => handleReject(tx.id, tx.needsReviewByUserId)}
                      className="text-sm bg-red-600 text-white px-3 py-1 rounded hover:bg-red-700 disabled:opacity-50"
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
                    className="text-sm bg-white border border-red-300 text-red-700 px-3 py-1 rounded hover:bg-red-50 disabled:opacity-50"
                    disabled={loadingId === tx.id}
                  >
                    Reject
                  </button>
                  <button
                    data-testid={`needs-review-reclassify-${tx.id}`}
                    onClick={() => setReclassifyId(tx.id)}
                    className="text-sm bg-white border border-gray-300 text-gray-700 px-3 py-1 rounded hover:bg-gray-50 disabled:opacity-50"
                    disabled={loadingId === tx.id}
                  >
                    Reclassify
                  </button>
                  <button
                    data-testid={`needs-review-approve-${tx.id}`}
                    onClick={() => handleApprove(tx.id)}
                    className="text-sm bg-green-600 text-white px-3 py-1 rounded hover:bg-green-700 disabled:opacity-50"
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
