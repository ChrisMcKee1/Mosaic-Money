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
        <h1 className="text-2xl font-semibold text-gray-900">Needs Review</h1>
        <p className="mt-1 text-sm text-gray-500">
          Transactions requiring human approval or classification.
        </p>
      </div>
      
      {error ? (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-md">
          {error}
        </div>
      ) : (
        <NeedsReviewList transactions={transactions} />
      )}
    </div>
  );
}
