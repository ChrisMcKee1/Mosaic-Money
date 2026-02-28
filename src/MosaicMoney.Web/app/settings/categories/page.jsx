import Link from "next/link";
import { ArrowLeft, FolderTree } from "lucide-react";
import { fetchApi } from "../../../lib/api";
import { CategoriesSettingsClient } from "./CategoriesSettingsClient";

export const metadata = {
  title: "Category Settings | Mosaic Money",
};

export const dynamic = "force-dynamic";

const SCOPE_ORDER = ["User", "HouseholdShared", "Platform"];

async function loadCategoriesForScope(scope) {
  try {
    const categories = await fetchApi(`/api/v1/categories?scope=${encodeURIComponent(scope)}`);

    return {
      categories: Array.isArray(categories) ? categories : [],
      loadError: null,
    };
  } catch (error) {
    console.error(`Failed to load categories for scope ${scope}.`, error);

    return {
      categories: [],
      loadError: `Unable to load ${scope} categories right now.`,
    };
  }
}

export default async function CategoriesSettingsPage() {
  const results = await Promise.all(SCOPE_ORDER.map((scope) => loadCategoriesForScope(scope)));

  const initialScopes = {
    User: results[0],
    HouseholdShared: results[1],
    Platform: results[2],
  };

  return (
    <div className="p-6 md:p-10 max-w-5xl w-full overflow-y-auto">
      <div className="mb-6">
        <Link
          href="/settings"
          className="inline-flex items-center text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to Settings
        </Link>
      </div>

      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-6 md:p-8">
        <div className="flex items-center gap-3 mb-2">
          <div className="p-2 rounded-lg bg-[var(--color-primary-hover)]/10 text-[var(--color-primary)]">
            <FolderTree className="h-6 w-6" />
          </div>
          <h1
            className="text-2xl md:text-3xl font-display font-bold text-[var(--color-text-main)]"
            data-testid="settings-categories-heading"
          >
            Category Taxonomy
          </h1>
        </div>
        <p className="text-sm text-[var(--color-text-muted)] mb-8">
          Manage category and subcategory lifecycle operations for your personal and household scopes. Platform taxonomy is visible here but remains operator-managed.
        </p>

        <CategoriesSettingsClient initialScopes={initialScopes} />
      </div>
    </div>
  );
}
