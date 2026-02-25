import { fetchApi } from "../../lib/api";
import NeedsReviewList from "./NeedsReviewList";

export const dynamic = "force-dynamic";

export default async function NeedsReviewPage() {
  let transactions = [];
  let error = null;

  try {
    transactions = await fetchApi("/api/v1/transactions?needsReviewOnly=true", {
      next: { tags: ["transactions", "needs-review"] },
    });
  } catch (e) {
    console.error("Failed to fetch needs-review transactions:", e);
    error = "Failed to load transactions. Please try again later.";
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-display font-semibold text-white">Needs Review</h1>
        <p className="mt-1 text-sm text-[var(--color-text-muted)]">
          Transactions requiring human approval or classification.
        </p>
      </div>
      
      {error ? (
        <div data-testid="needs-review-error-banner" className="bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] px-4 py-3 rounded-md">
          {error}
        </div>
      ) : (
        <NeedsReviewList transactions={transactions} />
      )}
    </div>
  );
}
