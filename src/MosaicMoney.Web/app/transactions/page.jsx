import { fetchApi } from "../../lib/api";
import { TransactionsClient } from "./TransactionsClient";

export const dynamic = "force-dynamic";

function parsePositiveInt(value, fallback) {
  const parsed = Number.parseInt(String(value ?? ""), 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallback;
  }

  return parsed;
}

export default async function TransactionsPage({ searchParams }) {
  const params = await searchParams;
  const page = parsePositiveInt(params?.page, 1);
  const pageSize = Math.min(parsePositiveInt(params?.pageSize, 100), 200);

  let transactions = [];
  let error = null;

  try {
    transactions = await fetchApi(
      `/api/v1/transactions/projection-metadata?page=${page}&pageSize=${pageSize}`,
    );
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

  return (
    <TransactionsClient
      initialTransactions={transactions}
      page={page}
      pageSize={pageSize}
      hasPreviousPage={page > 1}
      hasNextPage={transactions.length === pageSize}
    />
  );
}
