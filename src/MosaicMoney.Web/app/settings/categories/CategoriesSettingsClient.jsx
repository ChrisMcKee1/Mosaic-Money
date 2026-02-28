"use client";

import { useMemo, useState, useTransition } from "react";
import {
  archiveCategoryAction,
  archiveSubcategoryAction,
  createCategoryAction,
  createSubcategoryAction,
  renameCategoryAction,
  renameSubcategoryAction,
  reorderCategoriesAction,
  reorderSubcategoriesAction,
  reparentSubcategoryAction,
} from "./actions";

const SCOPE_CONFIG = [
  {
    key: "User",
    title: "My Categories",
    description: "Private categories visible only to your member profile.",
    mutable: true,
  },
  {
    key: "HouseholdShared",
    title: "Household Shared",
    description: "Shared categories available to every active member in your household.",
    mutable: true,
  },
  {
    key: "Platform",
    title: "Platform Baseline",
    description: "Managed by operators and read-only in settings.",
    mutable: false,
  },
];

const MUTATION_BLOCKED_MESSAGE = "Another change is in progress. Please wait for it to finish.";

function sortByDisplayOrder(items) {
  return [...items].sort((left, right) => {
    if (left.displayOrder !== right.displayOrder) {
      return left.displayOrder - right.displayOrder;
    }

    return left.name.localeCompare(right.name);
  });
}

export function CategoriesSettingsClient({ initialScopes }) {
  const [scopeState, setScopeState] = useState(initialScopes);
  const [activeScope, setActiveScope] = useState("User");
  const [isPending, startTransition] = useTransition();
  const [statusMessage, setStatusMessage] = useState(null);
  const [errorMessage, setErrorMessage] = useState(null);

  const [newCategoryName, setNewCategoryName] = useState("");
  const [editingCategoryId, setEditingCategoryId] = useState(null);
  const [editingCategoryName, setEditingCategoryName] = useState("");

  const [subcategoryDraftByCategoryId, setSubcategoryDraftByCategoryId] = useState({});
  const [subcategoryBusinessByCategoryId, setSubcategoryBusinessByCategoryId] = useState({});

  const [editingSubcategoryId, setEditingSubcategoryId] = useState(null);
  const [editingSubcategoryName, setEditingSubcategoryName] = useState("");
  const [editingSubcategoryBusiness, setEditingSubcategoryBusiness] = useState(false);

  const [movingSubcategoryId, setMovingSubcategoryId] = useState(null);
  const [moveTargetCategoryId, setMoveTargetCategoryId] = useState("");

  const currentScope = scopeState[activeScope] ?? { categories: [], loadError: null };
  const categories = useMemo(
    () => sortByDisplayOrder(Array.isArray(currentScope.categories) ? currentScope.categories : []),
    [currentScope.categories],
  );
  const scopeConfig = SCOPE_CONFIG.find((config) => config.key === activeScope) ?? SCOPE_CONFIG[0];

  function mergeScopeCategories(scope, categoriesForScope) {
    if (!categoriesForScope) {
      return;
    }

    setScopeState((previous) => ({
      ...previous,
      [scope]: {
        categories: sortByDisplayOrder(categoriesForScope),
        loadError: null,
      },
    }));
  }

  function clearEditingState() {
    setEditingCategoryId(null);
    setEditingCategoryName("");
    setEditingSubcategoryId(null);
    setEditingSubcategoryName("");
    setEditingSubcategoryBusiness(false);
    setMovingSubcategoryId(null);
    setMoveTargetCategoryId("");
  }

  function runMutation(callback) {
    if (isPending) {
      setErrorMessage(MUTATION_BLOCKED_MESSAGE);
      return;
    }

    const scopeAtStart = activeScope;

    setErrorMessage(null);
    setStatusMessage(null);

    startTransition(async () => {
      try {
        const result = await callback(scopeAtStart);

        if (!result?.success) {
          setErrorMessage(result?.error ?? "Failed to save taxonomy change.");
          return;
        }

        mergeScopeCategories(scopeAtStart, result.categories);
        clearEditingState();
        setStatusMessage(result.warning ?? result.message ?? "Saved.");
      } catch (error) {
        console.error("Category settings mutation failed.", error);
        setErrorMessage("Unexpected error while saving category settings.");
      }
    });
  }

  function handleCreateCategory() {
    runMutation(async (scope) => {
      const result = await createCategoryAction({
        scope,
        name: newCategoryName,
      });

      if (result.success) {
        setNewCategoryName("");
      }

      return result;
    });
  }

  function handleRenameCategory(categoryId) {
    runMutation((scope) =>
      renameCategoryAction({
        scope,
        categoryId,
        name: editingCategoryName,
      }),
    );
  }

  function handleArchiveCategory(categoryId, categoryName) {
    if (!confirm(`Archive category \"${categoryName}\" and all of its subcategories?`)) {
      return;
    }

    runMutation((scope) =>
      archiveCategoryAction({
        scope,
        categoryId,
        allowLinkedTransactions: true,
      }),
    );
  }

  function handleReorderCategory(index, direction) {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= categories.length) {
      return;
    }

    const nextIds = categories.map((category) => category.id);
    const currentId = nextIds[index];
    nextIds[index] = nextIds[nextIndex];
    nextIds[nextIndex] = currentId;

    runMutation((scope) =>
      reorderCategoriesAction({
        scope,
        categoryIds: nextIds,
      }),
    );
  }

  function handleCreateSubcategory(categoryId) {
    const draftName = subcategoryDraftByCategoryId[categoryId] ?? "";
    const isBusinessExpense = subcategoryBusinessByCategoryId[categoryId] === true;

    runMutation(async (scope) => {
      const result = await createSubcategoryAction({
        scope,
        categoryId,
        name: draftName,
        isBusinessExpense,
      });

      if (result.success) {
        setSubcategoryDraftByCategoryId((previous) => ({
          ...previous,
          [categoryId]: "",
        }));
        setSubcategoryBusinessByCategoryId((previous) => ({
          ...previous,
          [categoryId]: false,
        }));
      }

      return result;
    });
  }

  function handleRenameSubcategory(subcategoryId) {
    runMutation((scope) =>
      renameSubcategoryAction({
        scope,
        subcategoryId,
        name: editingSubcategoryName,
        isBusinessExpense: editingSubcategoryBusiness,
      }),
    );
  }

  function handleArchiveSubcategory(subcategoryId, subcategoryName) {
    if (!confirm(`Archive subcategory \"${subcategoryName}\"?`)) {
      return;
    }

    runMutation((scope) =>
      archiveSubcategoryAction({
        scope,
        subcategoryId,
        allowLinkedTransactions: true,
      }),
    );
  }

  function handleReparentSubcategory(subcategoryId) {
    runMutation((scope) =>
      reparentSubcategoryAction({
        scope,
        subcategoryId,
        targetCategoryId: moveTargetCategoryId,
      }),
    );
  }

  function handleReorderSubcategory(categoryId, index, direction, subcategories) {
    const nextIndex = index + direction;
    if (nextIndex < 0 || nextIndex >= subcategories.length) {
      return;
    }

    const nextIds = subcategories.map((sub) => sub.id);
    const currentId = nextIds[index];
    nextIds[index] = nextIds[nextIndex];
    nextIds[nextIndex] = currentId;

    runMutation((scope) =>
      reorderSubcategoriesAction({
        scope,
        categoryId,
        subcategoryIds: nextIds,
      }),
    );
  }

  return (
    <div className="space-y-6" data-testid="settings-categories-manager">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
        {SCOPE_CONFIG.map((scope) => {
          const isActive = scope.key === activeScope;
          return (
            <button
              key={scope.key}
              type="button"
              onClick={() => {
                setActiveScope(scope.key);
                clearEditingState();
                setErrorMessage(null);
                setStatusMessage(null);
              }}
              className={`rounded-xl border px-4 py-3 text-left transition-colors ${
                isActive
                  ? "border-[var(--color-primary)] bg-[var(--color-primary)]/10"
                  : "border-[var(--color-border)] bg-[var(--color-surface-hover)] hover:border-[var(--color-primary)]/40"
              }`}
            >
              <p className="text-sm font-semibold text-[var(--color-text-main)]">{scope.title}</p>
              <p className="mt-1 text-xs text-[var(--color-text-muted)]">{scope.description}</p>
            </button>
          );
        })}
      </div>

      {!scopeConfig.mutable && (
        <div className="rounded-md border border-[var(--color-warning)]/25 bg-[var(--color-warning-bg)] px-4 py-3 text-sm text-[var(--color-warning)]">
          Platform taxonomy is read-only from web settings. Use the operator lane for platform-managed changes.
        </div>
      )}

      {statusMessage && (
        <div className="rounded-md border border-[var(--color-positive)]/25 bg-[var(--color-positive-bg)] px-4 py-3 text-sm text-[var(--color-positive)]">
          {statusMessage}
        </div>
      )}

      {errorMessage && (
        <div className="rounded-md border border-[var(--color-negative)]/25 bg-[var(--color-negative-bg)] px-4 py-3 text-sm text-[var(--color-negative)]">
          {errorMessage}
        </div>
      )}

      {currentScope.loadError && (
        <div className="rounded-md border border-[var(--color-negative)]/25 bg-[var(--color-negative-bg)] px-4 py-3 text-sm text-[var(--color-negative)]">
          {currentScope.loadError}
        </div>
      )}

      {scopeConfig.mutable && (
        <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-4">
          <h2 className="text-base font-semibold text-[var(--color-text-main)]">Create Category</h2>
          <p className="mt-1 text-xs text-[var(--color-text-muted)]">
            Categories are scoped to {scopeConfig.title.toLowerCase()}.
          </p>
          <div className="mt-3 flex flex-col gap-3 sm:flex-row">
            <input
              type="text"
              aria-label="Category name"
              value={newCategoryName}
              onChange={(event) => setNewCategoryName(event.target.value)}
              placeholder="Category name"
              className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-main)] placeholder:text-[var(--color-text-subtle)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)]"
              disabled={isPending}
            />
            <button
              type="button"
              onClick={handleCreateCategory}
              disabled={isPending || newCategoryName.trim().length === 0}
              className="inline-flex items-center justify-center rounded-lg bg-[var(--color-primary)] px-4 py-2 text-sm font-medium text-[var(--color-background)] transition-colors hover:bg-[var(--color-primary-hover)] disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isPending ? "Saving..." : "Create"}
            </button>
          </div>
        </div>
      )}

      {categories.length === 0 && !currentScope.loadError ? (
        <div className="rounded-xl border border-dashed border-[var(--color-border)] bg-[var(--color-surface-hover)]/50 p-6 text-center">
          <p className="text-sm text-[var(--color-text-muted)]">No active categories in this scope yet.</p>
        </div>
      ) : (
        <div className="space-y-4">
          {categories.map((category, index) => {
            const isEditingCategory = editingCategoryId === category.id;
            const subcategories = sortByDisplayOrder(category.subcategories ?? []);

            return (
              <article
                key={category.id}
                className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] p-4"
                data-testid={`settings-category-${category.id}`}
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="flex-1">
                    {isEditingCategory ? (
                      <div className="flex flex-col gap-2 sm:flex-row">
                        <input
                          type="text"
                          aria-label="Category name"
                          value={editingCategoryName}
                          onChange={(event) => setEditingCategoryName(event.target.value)}
                          className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] px-3 py-2 text-sm text-[var(--color-text-main)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)]"
                          disabled={isPending}
                        />
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => handleRenameCategory(category.id)}
                            disabled={isPending || editingCategoryName.trim().length === 0}
                            className="rounded-lg bg-[var(--color-primary)] px-3 py-2 text-sm font-medium text-[var(--color-background)] hover:bg-[var(--color-primary-hover)] disabled:opacity-50"
                          >
                            Save
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              setEditingCategoryId(null);
                              setEditingCategoryName("");
                            }}
                            disabled={isPending}
                            className="rounded-lg border border-[var(--color-border)] px-3 py-2 text-sm text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]"
                          >
                            Cancel
                          </button>
                        </div>
                      </div>
                    ) : (
                      <>
                        <h3 className="text-base font-semibold text-[var(--color-text-main)]">{category.name}</h3>
                        <p className="mt-1 text-xs text-[var(--color-text-muted)]">
                          Order {category.displayOrder + 1} · {subcategories.length} subcategories
                        </p>
                      </>
                    )}
                  </div>

                  {scopeConfig.mutable && !isEditingCategory && (
                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        type="button"
                        onClick={() => handleReorderCategory(index, -1)}
                        disabled={isPending || index === 0}
                        className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] disabled:opacity-40"
                        title="Move up"
                      >
                        Up
                      </button>
                      <button
                        type="button"
                        onClick={() => handleReorderCategory(index, 1)}
                        disabled={isPending || index === categories.length - 1}
                        className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] disabled:opacity-40"
                        title="Move down"
                      >
                        Down
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setEditingCategoryId(category.id);
                          setEditingCategoryName(category.name);
                          setEditingSubcategoryId(null);
                          setMovingSubcategoryId(null);
                        }}
                        disabled={isPending}
                        className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]"
                      >
                        Rename
                      </button>
                      <button
                        type="button"
                        onClick={() => handleArchiveCategory(category.id, category.name)}
                        disabled={isPending}
                        className="rounded-lg border border-[var(--color-negative)]/50 px-2 py-1 text-xs text-[var(--color-negative)] hover:bg-[var(--color-negative-bg)]"
                      >
                        Archive
                      </button>
                    </div>
                  )}
                </div>

                <div className="mt-4 space-y-2 border-t border-[var(--color-border)]/70 pt-4">
                  {subcategories.length === 0 ? (
                    <p className="text-xs text-[var(--color-text-muted)]">No subcategories yet.</p>
                  ) : (
                    subcategories.map((subcategory, index) => {
                      const isEditingSubcategory = editingSubcategoryId === subcategory.id;
                      const isMovingSubcategory = movingSubcategoryId === subcategory.id;

                      return (
                        <div
                          key={subcategory.id}
                          className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] p-3"
                        >
                          {isEditingSubcategory ? (
                            <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                              <input
                                type="text"
                                aria-label="Subcategory name"
                                value={editingSubcategoryName}
                                onChange={(event) => setEditingSubcategoryName(event.target.value)}
                                className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-main)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)]"
                                disabled={isPending}
                              />
                              <label className="inline-flex items-center gap-2 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text-muted)]">
                                <input
                                  type="checkbox"
                                  checked={editingSubcategoryBusiness}
                                  onChange={(event) => setEditingSubcategoryBusiness(event.target.checked)}
                                  disabled={isPending}
                                />
                                Business
                              </label>
                              <div className="flex gap-2">
                                <button
                                  type="button"
                                  onClick={() => handleRenameSubcategory(subcategory.id)}
                                  disabled={isPending || editingSubcategoryName.trim().length === 0}
                                  className="rounded-lg bg-[var(--color-primary)] px-3 py-2 text-xs font-medium text-[var(--color-background)] hover:bg-[var(--color-primary-hover)] disabled:opacity-50"
                                >
                                  Save
                                </button>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setEditingSubcategoryId(null);
                                    setEditingSubcategoryName("");
                                    setEditingSubcategoryBusiness(false);
                                  }}
                                  disabled={isPending}
                                  className="rounded-lg border border-[var(--color-border)] px-3 py-2 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)]"
                                >
                                  Cancel
                                </button>
                              </div>
                            </div>
                          ) : (
                            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                              <div>
                                <p className="text-sm font-medium text-[var(--color-text-main)]">{subcategory.name}</p>
                                <p className="mt-1 text-xs text-[var(--color-text-muted)]">
                                  Order {subcategory.displayOrder + 1}
                                  {subcategory.isBusinessExpense ? " · Business expense" : " · Household expense"}
                                </p>
                              </div>

                              {scopeConfig.mutable && (
                                <div className="flex flex-wrap gap-2">
                                  <button
                                    type="button"
                                    onClick={() => handleReorderSubcategory(category.id, index, -1, subcategories)}
                                    disabled={isPending || index === 0}
                                    className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] disabled:opacity-40"
                                    title="Move up"
                                  >
                                    Up
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => handleReorderSubcategory(category.id, index, 1, subcategories)}
                                    disabled={isPending || index === subcategories.length - 1}
                                    className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] hover:text-[var(--color-text-main)] disabled:opacity-40"
                                    title="Move down"
                                  >
                                    Down
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => {
                                      setEditingSubcategoryId(subcategory.id);
                                      setEditingSubcategoryName(subcategory.name);
                                      setEditingSubcategoryBusiness(subcategory.isBusinessExpense);
                                      setMovingSubcategoryId(null);
                                    }}
                                    disabled={isPending}
                                    className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)]"
                                  >
                                    Rename
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => {
                                      setMovingSubcategoryId(subcategory.id);
                                      setMoveTargetCategoryId("");
                                      setEditingSubcategoryId(null);
                                    }}
                                    disabled={isPending || categories.length < 2}
                                    className="rounded-lg border border-[var(--color-border)] px-2 py-1 text-xs text-[var(--color-text-muted)] disabled:opacity-40"
                                  >
                                    Move
                                  </button>
                                  <button
                                    type="button"
                                    onClick={() => handleArchiveSubcategory(subcategory.id, subcategory.name)}
                                    disabled={isPending}
                                    className="rounded-lg border border-[var(--color-negative)]/50 px-2 py-1 text-xs text-[var(--color-negative)]"
                                  >
                                    Archive
                                  </button>
                                </div>
                              )}
                            </div>
                          )}

                          {isMovingSubcategory && scopeConfig.mutable && (
                            <div className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-center">
                              <select
                                value={moveTargetCategoryId}
                                onChange={(event) => setMoveTargetCategoryId(event.target.value)}
                                className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-xs text-[var(--color-text-main)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)]"
                                disabled={isPending}
                              >
                                <option value="">Select target category</option>
                                {categories
                                  .filter((candidate) => candidate.id !== category.id)
                                  .map((candidate) => (
                                    <option key={candidate.id} value={candidate.id}>
                                      {candidate.name}
                                    </option>
                                  ))}
                              </select>
                              <div className="flex gap-2">
                                <button
                                  type="button"
                                  onClick={() => handleReparentSubcategory(subcategory.id)}
                                  disabled={isPending || !moveTargetCategoryId}
                                  className="rounded-lg bg-[var(--color-primary)] px-3 py-2 text-xs font-medium text-[var(--color-background)] hover:bg-[var(--color-primary-hover)] disabled:opacity-50"
                                >
                                  Move
                                </button>
                                <button
                                  type="button"
                                  onClick={() => {
                                    setMovingSubcategoryId(null);
                                    setMoveTargetCategoryId("");
                                  }}
                                  disabled={isPending}
                                  className="rounded-lg border border-[var(--color-border)] px-3 py-2 text-xs text-[var(--color-text-muted)]"
                                >
                                  Cancel
                                </button>
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })
                  )}
                </div>

                {scopeConfig.mutable && (
                  <div className="mt-4 border-t border-[var(--color-border)]/70 pt-4">
                    <p className="text-xs font-medium text-[var(--color-text-main)]">Add Subcategory</p>
                    <div className="mt-2 flex flex-col gap-2 sm:flex-row">
                      <input
                        type="text"
                        aria-label="Subcategory name"
                        value={subcategoryDraftByCategoryId[category.id] ?? ""}
                        onChange={(event) =>
                          setSubcategoryDraftByCategoryId((previous) => ({
                            ...previous,
                            [category.id]: event.target.value,
                          }))
                        }
                        placeholder="Subcategory name"
                        className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] px-3 py-2 text-sm text-[var(--color-text-main)] placeholder:text-[var(--color-text-subtle)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-1 focus:ring-[var(--color-primary)]"
                        disabled={isPending}
                      />
                      <label className="inline-flex items-center gap-2 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-hover)] px-3 py-2 text-xs text-[var(--color-text-muted)]">
                        <input
                          type="checkbox"
                          checked={subcategoryBusinessByCategoryId[category.id] === true}
                          onChange={(event) =>
                            setSubcategoryBusinessByCategoryId((previous) => ({
                              ...previous,
                              [category.id]: event.target.checked,
                            }))
                          }
                          disabled={isPending}
                        />
                        Business
                      </label>
                      <button
                        type="button"
                        onClick={() => handleCreateSubcategory(category.id)}
                        disabled={isPending || !(subcategoryDraftByCategoryId[category.id] ?? "").trim()}
                        className="rounded-lg bg-[var(--color-primary)] px-3 py-2 text-sm font-medium text-[var(--color-background)] hover:bg-[var(--color-primary-hover)] disabled:opacity-50"
                      >
                        Add
                      </button>
                    </div>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}
