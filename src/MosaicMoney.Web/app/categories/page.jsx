import { fetchApi } from "../../lib/api";
import { CategoriesClient } from "./CategoriesClient";

export const dynamic = "force-dynamic";

export default async function CategoriesPage() {
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
        <div className="bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] p-4 rounded-md">
          Failed to load categories data: {error}
        </div>
      </div>
    );
  }

  return <CategoriesClient transactions={transactions} />;
}