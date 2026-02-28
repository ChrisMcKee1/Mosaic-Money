import { requestJson, toReadableError as toReadableErrorBase } from "../../../shared/services/mobileApiClient";
import type {
  CategoryLifecycleDto,
  CategoryScope,
  CreateCategoryRequest,
  CreateSubcategoryRequest,
  ReorderCategoriesRequest,
  ReparentSubcategoryRequest,
  UpdateCategoryRequest,
  UpdateSubcategoryRequest,
} from "../contracts/CategoryLifecycleContracts";

export function toReadableError(error: unknown): string {
  return toReadableErrorBase(error, "Unexpected error while updating category settings.");
}

export async function fetchCategories(scope: CategoryScope, includeArchived: boolean = false): Promise<CategoryLifecycleDto[]> {
  const params = new URLSearchParams({ scope });
  if (includeArchived) {
    params.set("includeArchived", "true");
  }

  const data = await requestJson<CategoryLifecycleDto[]>(`/api/v1/categories?${params.toString()}`);
  return Array.isArray(data) ? data : [];
}

export async function createCategory(request: CreateCategoryRequest): Promise<CategoryLifecycleDto> {
  return requestJson<CategoryLifecycleDto, CreateCategoryRequest>("/api/v1/categories", {
    method: "POST",
    body: request,
  });
}

export async function updateCategory(categoryId: string, request: UpdateCategoryRequest): Promise<CategoryLifecycleDto> {
  return requestJson<CategoryLifecycleDto, UpdateCategoryRequest>(`/api/v1/categories/${encodeURIComponent(categoryId)}`, {
    method: "PATCH",
    body: request,
  });
}

export async function archiveCategory(categoryId: string, allowLinkedTransactions: boolean = true): Promise<CategoryLifecycleDto> {
  return requestJson<CategoryLifecycleDto>(
    `/api/v1/categories/${encodeURIComponent(categoryId)}?allowLinkedTransactions=${allowLinkedTransactions}`,
    { method: "DELETE" },
  );
}

export async function reorderCategories(request: ReorderCategoriesRequest): Promise<CategoryLifecycleDto[]> {
  return requestJson<CategoryLifecycleDto[], ReorderCategoriesRequest>("/api/v1/categories/reorder", {
    method: "POST",
    body: request,
  });
}

export async function createSubcategory(request: CreateSubcategoryRequest): Promise<void> {
  await requestJson<void, CreateSubcategoryRequest>("/api/v1/subcategories", {
    method: "POST",
    body: request,
  });
}

export async function updateSubcategory(subcategoryId: string, request: UpdateSubcategoryRequest): Promise<void> {
  await requestJson<void, UpdateSubcategoryRequest>(`/api/v1/subcategories/${encodeURIComponent(subcategoryId)}`, {
    method: "PATCH",
    body: request,
  });
}

export async function archiveSubcategory(subcategoryId: string, allowLinkedTransactions: boolean = true): Promise<void> {
  await requestJson<void>(
    `/api/v1/subcategories/${encodeURIComponent(subcategoryId)}?allowLinkedTransactions=${allowLinkedTransactions}`,
    { method: "DELETE" },
  );
}

export async function reparentSubcategory(subcategoryId: string, request: ReparentSubcategoryRequest): Promise<void> {
  await requestJson<void, ReparentSubcategoryRequest>(`/api/v1/subcategories/${encodeURIComponent(subcategoryId)}/reparent`, {
    method: "POST",
    body: request,
  });
}

export async function reorderSubcategories(subcategoryIds: string[]): Promise<void> {
  for (let index = 0; index < subcategoryIds.length; index += 1) {
    const subcategoryId = subcategoryIds[index];
    await updateSubcategory(subcategoryId, { displayOrder: index });
  }
}
