import { fetchApi } from "../../lib/api";
import { TransactionsClient } from "./TransactionsClient";

export const dynamic = "force-dynamic";

export default async function TransactionsPage() {
  let transactions = [];
  let error = null;

  try {
    transactions = await fetchApi("/api/v1/transactions/projection-metadata?pageSize=200");
  } catch (e) {
    error = e.message;
  }

  if (error) {
    return (
      <div className="p-8">
        <div data-testid="transactions-error-banner" className="bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] p-4 rounded-md">
          Failed to load transactions: {error}
        </div>
      </div>
    );
  }

  return <TransactionsClient initialTransactions={transactions} />;
}
