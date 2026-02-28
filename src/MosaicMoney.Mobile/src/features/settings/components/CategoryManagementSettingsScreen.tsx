import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { theme } from "../../../theme/tokens";
import type { CategoryLifecycleDto, CategoryScope, CategorySubcategoryDto } from "../contracts/CategoryLifecycleContracts";
import {
  archiveCategory,
  archiveSubcategory,
  createCategory,
  createSubcategory,
  fetchCategories,
  reorderCategories,
  reorderSubcategories,
  reparentSubcategory,
  toReadableError,
  updateCategory,
  updateSubcategory,
} from "../services/CategoryApiService";
import {
  enqueueCategoryMutation,
  listCategoryMutationQueueEntries,
  listCategoryMutationReconciliationNotices,
} from "../offline/categoryMutationQueue";
import { MobileApiError } from "../../../shared/services/mobileApiClient";

const SCOPE_TABS: CategoryScope[] = ["User", "HouseholdShared", "Platform"];

function isMutableScope(scope: CategoryScope): boolean {
  return scope !== "Platform";
}

function buildReplayKey(method: "POST" | "PATCH" | "DELETE", path: string, body?: unknown): string {
  return `${method}|${path}|${JSON.stringify(body ?? null)}`;
}

function isRetriableMutationError(error: unknown): boolean {
  if (error instanceof MobileApiError) {
    return error.status === 408 || error.status === 429 || error.status >= 500;
  }

  return error instanceof TypeError;
}

function sortCategories(categories: CategoryLifecycleDto[]): CategoryLifecycleDto[] {
  return [...categories]
    .map((category) => ({
      ...category,
      subcategories: [...category.subcategories].sort((a, b) => {
        if (a.displayOrder !== b.displayOrder) {
          return a.displayOrder - b.displayOrder;
        }

        return a.name.localeCompare(b.name);
      }),
    }))
    .sort((a, b) => {
      if (a.displayOrder !== b.displayOrder) {
        return a.displayOrder - b.displayOrder;
      }

      return a.name.localeCompare(b.name);
    });
}

export function CategoryManagementSettingsScreen() {
  const [activeScope, setActiveScope] = useState<CategoryScope>("User");
  const [categories, setCategories] = useState<CategoryLifecycleDto[]>([]);
  const [newCategoryName, setNewCategoryName] = useState("");
  const [renameCategoryDrafts, setRenameCategoryDrafts] = useState<Record<string, string>>({});
  const [newSubcategoryDrafts, setNewSubcategoryDrafts] = useState<Record<string, string>>({});
  const [renameSubcategoryDrafts, setRenameSubcategoryDrafts] = useState<Record<string, string>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [queuedMutationCount, setQueuedMutationCount] = useState(0);
  const [reconciliationNoticeCount, setReconciliationNoticeCount] = useState(0);

  const mutableScope = useMemo(() => isMutableScope(activeScope), [activeScope]);

  const refreshQueueMetrics = useCallback(async () => {
    const [queueEntries, reconciliationNotices] = await Promise.all([
      listCategoryMutationQueueEntries(),
      listCategoryMutationReconciliationNotices(),
    ]);

    setQueuedMutationCount(queueEntries.length);
    setReconciliationNoticeCount(reconciliationNotices.length);
  }, []);

  const loadCategories = useCallback(async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const data = await fetchCategories(activeScope, false);
      setCategories(sortCategories(data));
      await refreshQueueMetrics();
    } catch (error) {
      setErrorMessage(toReadableError(error));
    } finally {
      setIsLoading(false);
    }
  }, [activeScope, refreshQueueMetrics]);

  useEffect(() => {
    void loadCategories();
  }, [loadCategories]);

  const runMutation = useCallback(
    async (input: {
      summary: string;
      method: "POST" | "PATCH" | "DELETE";
      path: string;
      body?: unknown;
      operation: () => Promise<void>;
    }) => {
      setIsSubmitting(true);
      setErrorMessage(null);
      setStatusMessage(null);

      try {
        await input.operation();
        setStatusMessage(input.summary);
        await loadCategories();
      } catch (error) {
        if (isRetriableMutationError(error)) {
          await enqueueCategoryMutation({
            method: input.method,
            path: input.path,
            body: input.body,
            scope: activeScope,
            replayKey: buildReplayKey(input.method, input.path, input.body),
            summary: input.summary,
          });
          setStatusMessage(`${input.summary} Saved offline and queued for replay.`);
          await refreshQueueMetrics();
        } else {
          setErrorMessage(toReadableError(error));
        }
      } finally {
        setIsSubmitting(false);
      }
    },
    [activeScope, loadCategories, refreshQueueMetrics],
  );

  const handleCreateCategory = useCallback(async () => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const trimmedName = newCategoryName.trim();
    if (!trimmedName) {
      setErrorMessage("Category name is required.");
      return;
    }

    const body = {
      name: trimmedName,
      scope: activeScope,
    };

    await runMutation({
      summary: `Created category "${trimmedName}".`,
      method: "POST",
      path: "/api/v1/categories",
      body,
      operation: async () => {
        await createCategory(body);
      },
    });

    setNewCategoryName("");
  }, [activeScope, mutableScope, isSubmitting, newCategoryName, runMutation]);

  const handleRenameCategory = useCallback(async (category: CategoryLifecycleDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const draftName = renameCategoryDrafts[category.id] ?? "";
    const trimmedName = draftName.trim();
    if (!trimmedName) {
      setErrorMessage("Category name is required.");
      return;
    }

    const path = `/api/v1/categories/${encodeURIComponent(category.id)}`;
    const body = { name: trimmedName };

    await runMutation({
      summary: `Renamed category to "${trimmedName}".`,
      method: "PATCH",
      path,
      body,
      operation: async () => {
        await updateCategory(category.id, body);
      },
    });

    setRenameCategoryDrafts((current) => ({
      ...current,
      [category.id]: trimmedName,
    }));
  }, [isSubmitting, mutableScope, renameCategoryDrafts, runMutation]);

  const handleArchiveCategory = useCallback((category: CategoryLifecycleDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    Alert.alert(
      "Archive category?",
      `Archive "${category.name}" and all active subcategories?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Archive",
          style: "destructive",
          onPress: () => {
            void runMutation({
              summary: `Archived category "${category.name}".`,
              method: "DELETE",
              path: `/api/v1/categories/${encodeURIComponent(category.id)}?allowLinkedTransactions=true`,
              operation: async () => {
                await archiveCategory(category.id, true);
              },
            });
          },
        },
      ],
    );
  }, [isSubmitting, mutableScope, runMutation]);

  const handleMoveCategory = useCallback(async (categoryId: string, direction: -1 | 1) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const ordered = sortCategories(categories);
    const currentIndex = ordered.findIndex((category) => category.id === categoryId);
    if (currentIndex < 0) {
      return;
    }

    const targetIndex = currentIndex + direction;
    if (targetIndex < 0 || targetIndex >= ordered.length) {
      return;
    }

    const reordered = [...ordered];
    const [movedCategory] = reordered.splice(currentIndex, 1);
    reordered.splice(targetIndex, 0, movedCategory);

    const categoryIds = reordered.map((category) => category.id);
    const expectedLastModifiedAtUtc = ordered.reduce<string | null>((latest, category) => {
      if (!latest || category.lastModifiedAtUtc > latest) {
        return category.lastModifiedAtUtc;
      }

      return latest;
    }, null);

    const body = {
      scope: activeScope,
      categoryIds,
      expectedLastModifiedAtUtc,
    };

    await runMutation({
      summary: "Updated category ordering.",
      method: "POST",
      path: "/api/v1/categories/reorder",
      body,
      operation: async () => {
        await reorderCategories(body);
      },
    });
  }, [activeScope, categories, isSubmitting, mutableScope, runMutation]);

  const handleCreateSubcategory = useCallback(async (category: CategoryLifecycleDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const draftName = newSubcategoryDrafts[category.id] ?? "";
    const trimmedName = draftName.trim();
    if (!trimmedName) {
      setErrorMessage("Subcategory name is required.");
      return;
    }

    const body = {
      categoryId: category.id,
      name: trimmedName,
      isBusinessExpense: false,
    };

    await runMutation({
      summary: `Created subcategory "${trimmedName}".`,
      method: "POST",
      path: "/api/v1/subcategories",
      body,
      operation: async () => {
        await createSubcategory(body);
      },
    });

    setNewSubcategoryDrafts((current) => ({
      ...current,
      [category.id]: "",
    }));
  }, [isSubmitting, mutableScope, newSubcategoryDrafts, runMutation]);

  const handleRenameSubcategory = useCallback(async (subcategory: CategorySubcategoryDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const draftName = renameSubcategoryDrafts[subcategory.id] ?? "";
    const trimmedName = draftName.trim();
    if (!trimmedName) {
      setErrorMessage("Subcategory name is required.");
      return;
    }

    const path = `/api/v1/subcategories/${encodeURIComponent(subcategory.id)}`;
    const body = { name: trimmedName };

    await runMutation({
      summary: `Renamed subcategory to "${trimmedName}".`,
      method: "PATCH",
      path,
      body,
      operation: async () => {
        await updateSubcategory(subcategory.id, body);
      },
    });

    setRenameSubcategoryDrafts((current) => ({
      ...current,
      [subcategory.id]: trimmedName,
    }));
  }, [isSubmitting, mutableScope, renameSubcategoryDrafts, runMutation]);

  const handleToggleBusinessExpense = useCallback(async (subcategory: CategorySubcategoryDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const path = `/api/v1/subcategories/${encodeURIComponent(subcategory.id)}`;
    const body = { isBusinessExpense: !subcategory.isBusinessExpense };

    await runMutation({
      summary: `Updated business expense flag for "${subcategory.name}".`,
      method: "PATCH",
      path,
      body,
      operation: async () => {
        await updateSubcategory(subcategory.id, body);
      },
    });
  }, [isSubmitting, mutableScope, runMutation]);

  const handleArchiveSubcategory = useCallback((subcategory: CategorySubcategoryDto) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    Alert.alert(
      "Archive subcategory?",
      `Archive "${subcategory.name}"?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Archive",
          style: "destructive",
          onPress: () => {
            void runMutation({
              summary: `Archived subcategory "${subcategory.name}".`,
              method: "DELETE",
              path: `/api/v1/subcategories/${encodeURIComponent(subcategory.id)}?allowLinkedTransactions=true`,
              operation: async () => {
                await archiveSubcategory(subcategory.id, true);
              },
            });
          },
        },
      ],
    );
  }, [isSubmitting, mutableScope, runMutation]);

  const handleMoveSubcategory = useCallback(async (
    category: CategoryLifecycleDto,
    subcategoryId: string,
    direction: -1 | 1,
  ) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const ordered = [...category.subcategories].sort((a, b) => a.displayOrder - b.displayOrder);
    const currentIndex = ordered.findIndex((subcategory) => subcategory.id === subcategoryId);
    if (currentIndex < 0) {
      return;
    }

    const targetIndex = currentIndex + direction;
    if (targetIndex < 0 || targetIndex >= ordered.length) {
      return;
    }

    const reordered = [...ordered];
    const [movedSubcategory] = reordered.splice(currentIndex, 1);
    reordered.splice(targetIndex, 0, movedSubcategory);

    const subcategoryPatchQueue = reordered.map((subcategory, index) => ({
      method: "PATCH" as const,
      path: `/api/v1/subcategories/${encodeURIComponent(subcategory.id)}`,
      body: { displayOrder: index },
    }));

    setIsSubmitting(true);
    setErrorMessage(null);
    setStatusMessage(null);

    try {
      await reorderSubcategories(reordered.map((subcategory) => subcategory.id));
      setStatusMessage(`Updated subcategory ordering for "${category.name}".`);
      await loadCategories();
    } catch (error) {
      if (isRetriableMutationError(error)) {
        for (const request of subcategoryPatchQueue) {
          await enqueueCategoryMutation({
            method: request.method,
            path: request.path,
            body: request.body,
            scope: activeScope,
            replayKey: buildReplayKey(request.method, request.path, request.body),
            summary: `Replay subcategory reorder for "${category.name}".`,
          });
        }

        setStatusMessage(
          `Updated subcategory ordering for "${category.name}". Saved offline and queued for replay.`,
        );
        await refreshQueueMetrics();
      } else {
        setErrorMessage(toReadableError(error));
      }
    } finally {
      setIsSubmitting(false);
    }
  }, [activeScope, isSubmitting, loadCategories, mutableScope, refreshQueueMetrics]);

  const handleReparentSubcategory = useCallback((
    category: CategoryLifecycleDto,
    subcategory: CategorySubcategoryDto,
  ) => {
    if (!mutableScope || isSubmitting) {
      return;
    }

    const targetCategories = categories.filter((candidate) => candidate.id !== category.id);
    if (targetCategories.length === 0) {
      setErrorMessage("Create another category before moving this subcategory.");
      return;
    }

    Alert.alert(
      `Move "${subcategory.name}"`,
      "Choose a target category:",
      [
        ...targetCategories.map((target) => ({
          text: target.name,
          onPress: () => {
            const body = { targetCategoryId: target.id };
            void runMutation({
              summary: `Moved "${subcategory.name}" to "${target.name}".`,
              method: "POST",
              path: `/api/v1/subcategories/${encodeURIComponent(subcategory.id)}/reparent`,
              body,
              operation: async () => {
                await reparentSubcategory(subcategory.id, body);
              },
            });
          },
        })),
        { text: "Cancel", style: "cancel" as const },
      ],
    );
  }, [categories, isSubmitting, mutableScope, runMutation]);

  return (
    <View style={styles.container}>
      <View style={styles.tabs}>
        {SCOPE_TABS.map((scope) => (
          <Pressable
            key={scope}
            style={[styles.tab, activeScope === scope && styles.tabActive]}
            disabled={isSubmitting}
            onPress={() => {
              setActiveScope(scope);
              setStatusMessage(null);
              setErrorMessage(null);
            }}
          >
            <Text style={[styles.tabText, activeScope === scope && styles.tabTextActive]}>
              {scope}
            </Text>
          </Pressable>
        ))}
      </View>

      {!mutableScope ? (
        <View style={styles.banner}>
          <Text style={styles.bannerText}>Platform scope is read-only in mobile settings.</Text>
        </View>
      ) : null}

      <View style={styles.bannerMetrics}>
        <Text style={styles.bannerMetricsText}>Pending sync queue: {queuedMutationCount}</Text>
        <Text style={styles.bannerMetricsText}>Reconciliation notices: {reconciliationNoticeCount}</Text>
      </View>

      {statusMessage ? <Text style={styles.statusText}>{statusMessage}</Text> : null}
      {errorMessage ? <Text style={styles.errorText}>{errorMessage}</Text> : null}

      {mutableScope ? (
        <View style={styles.createCard}>
          <Text style={styles.formLabel}>Create Category</Text>
          <TextInput
            style={styles.input}
            placeholder="Category name"
            placeholderTextColor={theme.colors.textSubtle}
            value={newCategoryName}
            editable={!isSubmitting}
            onChangeText={setNewCategoryName}
          />
          <Pressable
            style={[styles.actionButton, isSubmitting && styles.disabledButton]}
            disabled={isSubmitting}
            onPress={() => {
              void handleCreateCategory();
            }}
          >
            <Text style={styles.actionButtonText}>Create Category</Text>
          </Pressable>
        </View>
      ) : null}

      <ScrollView
        style={styles.scroll}
        contentContainerStyle={styles.scrollContent}
        refreshControl={<RefreshControl refreshing={isLoading} onRefresh={() => { void loadCategories(); }} />}
      >
        {isLoading ? (
          <ActivityIndicator size="large" color={theme.colors.primary} />
        ) : categories.length === 0 ? (
          <Text style={styles.emptyText}>No categories found for this scope.</Text>
        ) : (
          categories.map((category) => (
            <View key={category.id} style={styles.categoryCard}>
              <View style={styles.categoryHeader}>
                <Text style={styles.categoryTitle}>{category.name}</Text>
                <Text style={styles.categoryMeta}>#{category.displayOrder}</Text>
              </View>

              {mutableScope ? (
                <>
                  <View style={styles.row}>
                    <TextInput
                      style={[styles.input, styles.inlineInput]}
                      placeholder={category.name}
                      placeholderTextColor={theme.colors.textSubtle}
                      editable={!isSubmitting}
                      value={renameCategoryDrafts[category.id] ?? ""}
                      onChangeText={(value) => {
                        setRenameCategoryDrafts((current) => ({
                          ...current,
                          [category.id]: value,
                        }));
                      }}
                    />
                    <Pressable
                      style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                      disabled={isSubmitting}
                      onPress={() => {
                        void handleRenameCategory(category);
                      }}
                    >
                      <Text style={styles.smallButtonText}>Rename</Text>
                    </Pressable>
                  </View>

                  <View style={styles.row}>
                    <Pressable
                      style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                      disabled={isSubmitting}
                      onPress={() => {
                        void handleMoveCategory(category.id, -1);
                      }}
                    >
                      <Text style={styles.smallButtonText}>Move Up</Text>
                    </Pressable>
                    <Pressable
                      style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                      disabled={isSubmitting}
                      onPress={() => {
                        void handleMoveCategory(category.id, 1);
                      }}
                    >
                      <Text style={styles.smallButtonText}>Move Down</Text>
                    </Pressable>
                    <Pressable
                      style={[styles.smallButtonArchive, isSubmitting && styles.disabledButton]}
                      disabled={isSubmitting}
                      onPress={() => handleArchiveCategory(category)}
                    >
                      <Text style={styles.smallButtonArchiveText}>Archive</Text>
                    </Pressable>
                  </View>

                  <View style={styles.row}>
                    <TextInput
                      style={[styles.input, styles.inlineInput]}
                      placeholder="New subcategory"
                      placeholderTextColor={theme.colors.textSubtle}
                      editable={!isSubmitting}
                      value={newSubcategoryDrafts[category.id] ?? ""}
                      onChangeText={(value) => {
                        setNewSubcategoryDrafts((current) => ({
                          ...current,
                          [category.id]: value,
                        }));
                      }}
                    />
                    <Pressable
                      style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                      disabled={isSubmitting}
                      onPress={() => {
                        void handleCreateSubcategory(category);
                      }}
                    >
                      <Text style={styles.smallButtonText}>Add Sub</Text>
                    </Pressable>
                  </View>
                </>
              ) : null}

              {category.subcategories.length > 0 ? (
                <View style={styles.subList}>
                  {category.subcategories.map((subcategory) => (
                    <View key={subcategory.id} style={styles.subCard}>
                      <View style={styles.subHeader}>
                        <Text style={styles.subTitle}>{subcategory.name}</Text>
                        <Text style={styles.categoryMeta}>#{subcategory.displayOrder}</Text>
                      </View>

                      <Text style={styles.subMeta}>
                        {subcategory.isBusinessExpense ? "Business expense" : "Personal expense"}
                      </Text>

                      {mutableScope ? (
                        <>
                          <View style={styles.row}>
                            <TextInput
                              style={[styles.input, styles.inlineInput]}
                              placeholder={subcategory.name}
                              placeholderTextColor={theme.colors.textSubtle}
                              editable={!isSubmitting}
                              value={renameSubcategoryDrafts[subcategory.id] ?? ""}
                              onChangeText={(value) => {
                                setRenameSubcategoryDrafts((current) => ({
                                  ...current,
                                  [subcategory.id]: value,
                                }));
                              }}
                            />
                            <Pressable
                              style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => {
                                void handleRenameSubcategory(subcategory);
                              }}
                            >
                              <Text style={styles.smallButtonText}>Rename</Text>
                            </Pressable>
                          </View>

                          <View style={styles.row}>
                            <Pressable
                              style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => {
                                void handleMoveSubcategory(category, subcategory.id, -1);
                              }}
                            >
                              <Text style={styles.smallButtonText}>Up</Text>
                            </Pressable>
                            <Pressable
                              style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => {
                                void handleMoveSubcategory(category, subcategory.id, 1);
                              }}
                            >
                              <Text style={styles.smallButtonText}>Down</Text>
                            </Pressable>
                            <Pressable
                              style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => {
                                void handleToggleBusinessExpense(subcategory);
                              }}
                            >
                              <Text style={styles.smallButtonText}>Toggle Biz</Text>
                            </Pressable>
                          </View>

                          <View style={styles.row}>
                            <Pressable
                              style={[styles.smallButton, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => handleReparentSubcategory(category, subcategory)}
                            >
                              <Text style={styles.smallButtonText}>Move Category</Text>
                            </Pressable>
                            <Pressable
                              style={[styles.smallButtonArchive, isSubmitting && styles.disabledButton]}
                              disabled={isSubmitting}
                              onPress={() => handleArchiveSubcategory(subcategory)}
                            >
                              <Text style={styles.smallButtonArchiveText}>Archive</Text>
                            </Pressable>
                          </View>
                        </>
                      ) : null}
                    </View>
                  ))}
              </View>
              ) : (
                <Text style={styles.emptySubText}>No active subcategories.</Text>
              )}
            </View>
          ))
        )}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: theme.colors.background,
  },
  tabs: {
    flexDirection: "row",
    borderBottomWidth: 1,
    borderColor: theme.colors.border,
    backgroundColor: theme.colors.surface,
  },
  tab: {
    flex: 1,
    paddingVertical: 14,
    alignItems: "center",
  },
  tabActive: {
    borderBottomWidth: 2,
    borderBottomColor: theme.colors.primary,
  },
  tabText: {
    fontSize: 14,
    fontWeight: "600",
    color: theme.colors.textMuted,
  },
  tabTextActive: {
    color: theme.colors.primary,
  },
  banner: {
    backgroundColor: theme.colors.surfaceHover,
    padding: 12,
    borderBottomWidth: 1,
    borderColor: theme.colors.border,
  },
  bannerMetrics: {
    backgroundColor: theme.colors.surface,
    borderBottomColor: theme.colors.border,
    borderBottomWidth: 1,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  bannerMetricsText: {
    color: theme.colors.textMuted,
    fontSize: 12,
  },
  bannerText: {
    color: theme.colors.textMain,
    fontSize: 13,
  },
  statusText: {
    color: theme.colors.positive,
    fontSize: 13,
    paddingHorizontal: 16,
    paddingTop: 10,
  },
  createCard: {
    paddingHorizontal: 16,
    paddingTop: 12,
  },
  formLabel: {
    color: theme.colors.textMain,
    fontSize: 13,
    fontWeight: "600",
    marginBottom: 6,
  },
  input: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    color: theme.colors.textMain,
    fontSize: 14,
    marginBottom: 8,
    minHeight: 40,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  inlineInput: {
    flex: 1,
    marginBottom: 0,
  },
  actionButton: {
    backgroundColor: theme.colors.primary,
    borderRadius: theme.borderRadius.md,
    alignItems: "center",
    justifyContent: "center",
    minHeight: 40,
    paddingHorizontal: 14,
  },
  actionButtonText: {
    color: theme.colors.background,
    fontWeight: "600",
    fontSize: 14,
  },
  disabledButton: {
    opacity: 0.55,
  },
  scroll: {
    flex: 1,
  },
  scrollContent: {
    padding: 16,
    gap: 12,
  },
  errorText: {
    color: theme.colors.warning,
    fontSize: 13,
    paddingHorizontal: 16,
    paddingTop: 10,
  },
  emptyText: {
    color: theme.colors.textMuted,
    textAlign: "center",
  },
  categoryCard: {
    backgroundColor: theme.colors.surface,
    padding: 16,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    borderColor: theme.colors.border,
  },
  categoryHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 8,
  },
  categoryTitle: {
    fontSize: 16,
    fontWeight: "700",
    color: theme.colors.textMain,
    flex: 1,
  },
  categoryMeta: {
    color: theme.colors.textMuted,
    fontSize: 12,
    fontWeight: "600",
    marginLeft: 8,
  },
  row: {
    flexDirection: "row",
    gap: 8,
    marginBottom: 8,
  },
  smallButton: {
    backgroundColor: theme.colors.surfaceHover,
    alignItems: "center",
    borderRadius: theme.borderRadius.sm,
    justifyContent: "center",
    minHeight: 36,
    paddingHorizontal: 10,
  },
  smallButtonText: {
    fontSize: 11,
    fontWeight: "600",
    color: theme.colors.primary,
  },
  smallButtonArchive: {
    alignItems: "center",
    backgroundColor: theme.colors.warningBg,
    borderRadius: theme.borderRadius.sm,
    justifyContent: "center",
    minHeight: 36,
    paddingHorizontal: 10,
  },
  smallButtonArchiveText: {
    fontSize: 11,
    fontWeight: "600",
    color: theme.colors.warning,
  },
  subList: {
    marginTop: 12,
    gap: 8,
    paddingLeft: 12,
    borderLeftWidth: 2,
    borderColor: theme.colors.border,
  },
  subCard: {
    backgroundColor: theme.colors.surfaceHover,
    padding: 10,
    borderRadius: theme.borderRadius.sm,
  },
  subHeader: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  subTitle: {
    fontSize: 14,
    fontWeight: "600",
    color: theme.colors.textMain,
  },
  subMeta: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginBottom: 8,
  },
  emptySubText: {
    color: theme.colors.textSubtle,
    fontSize: 12,
    fontStyle: "italic",
    marginTop: 4,
  },
});
