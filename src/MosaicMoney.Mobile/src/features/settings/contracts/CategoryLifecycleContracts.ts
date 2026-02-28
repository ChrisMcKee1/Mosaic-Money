export type CategoryScope = "User" | "HouseholdShared" | "Platform";

export interface CategorySubcategoryDto {
  id: string;
  categoryId: string;
  name: string;
  isBusinessExpense: boolean;
  displayOrder: number;
  isArchived: boolean;
  createdAtUtc: string;
  lastModifiedAtUtc: string;
  archivedAtUtc: string | null;
}

export interface CategoryLifecycleDto {
  id: string;
  name: string;
  displayOrder: number;
  isSystem: boolean;
  ownerType: CategoryScope | string;
  householdId: string | null;
  ownerUserId: string | null;
  isArchived: boolean;
  createdAtUtc: string;
  lastModifiedAtUtc: string;
  archivedAtUtc: string | null;
  subcategories: CategorySubcategoryDto[];
}

export interface CreateCategoryRequest {
  name: string;
  scope: CategoryScope;
  displayOrder?: number;
}

export interface UpdateCategoryRequest {
  name?: string;
}

export interface ReorderCategoriesRequest {
  scope: CategoryScope;
  categoryIds: string[];
  expectedLastModifiedAtUtc?: string | null;
}

export interface CreateSubcategoryRequest {
  categoryId: string;
  name: string;
  isBusinessExpense: boolean;
  displayOrder?: number;
}

export interface UpdateSubcategoryRequest {
  name?: string;
  isBusinessExpense?: boolean;
  displayOrder?: number;
}

export interface ReparentSubcategoryRequest {
  targetCategoryId: string;
  displayOrder?: number;
}

export interface QueuedCategoryMutationRequest {
  method: "POST" | "PATCH" | "DELETE";
  path: string;
  body?: unknown;
  scope: CategoryScope;
  replayKey: string;
  summary: string;
}
