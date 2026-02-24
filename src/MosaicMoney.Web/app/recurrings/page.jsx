import { fetchApi } from "../../lib/api";
import { RecurringsClient } from "./RecurringsClient";

export const dynamic = "force-dynamic";

export default async function RecurringsPage() {
  let recurringItems = [];
  let error = null;

  try {
    recurringItems = await fetchApi("/api/v1/recurring?isActive=true");
  } catch (e) {
    error = e.message;
  }

  if (error) {
    return (
      <div className="p-8">
        <div className="bg-[var(--color-negative-bg)] border border-[var(--color-negative)] text-[var(--color-negative)] p-4 rounded-md">
          Failed to load recurring items: {error}
        </div>
      </div>
    );
  }

  return <RecurringsClient items={recurringItems} />;
}