"use server";

import { auth } from "@clerk/nextjs/server";
import { revalidatePath } from "next/cache";
import { getApiBaseUrl } from "../../../lib/api";

const MUTABLE_SCOPES = new Set(["User", "HouseholdShared"]);
const VALID_SCOPES = new Set(["User", "HouseholdShared", "Platform"]);

function normalizeScope(scope) {
  if (!VALID_SCOPES.has(scope)) {
    return "User";
  }

  return scope;
}

function parseApiError(payload, fallbackMessage) {
  if (!payload || typeof payload !== "object") {
    return fallbackMessage;
  }

  if (payload.error?.details?.length) {
    return payload.error.details
      .map((detail) => `${detail.field}: ${detail.message}`)
      .join(" ");
  }

  if (payload.error?.message) {
    return payload.error.message;
  }

  if (typeof payload.error === "string") {
    return payload.error;
  }

  return fallbackMessage;
}

async function getAuthorizationHeader() {
  const isClerkConfigured = !!process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY && !!process.env.CLERK_SECRET_KEY;

  if (!isClerkConfigured) {
    return {};
  }

  try {
    const authContext = await auth();
    const token = await authContext.getToken();

    if (!token) {
      return {};
    }

    return { Authorization: `Bearer ${token}` };
  } catch (error) {
    console.error("Failed to resolve Clerk bearer token for taxonomy action.", error);
    return {};
  }
}

async function callTaxonomyApi(path, options = {}) {
  const baseUrl = getApiBaseUrl();
  const authorizationHeader = await getAuthorizationHeader();
  const householdUserId = process.env.MOSAIC_HOUSEHOLD_USER_ID?.trim();
  const identityHeader = householdUserId ? { "X-Mosaic-Household-User-Id": householdUserId } : {};

  const response = await fetch(`${baseUrl}${path}`, {
    method: options.method ?? "GET",
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
      ...authorizationHeader,
      ...identityHeader,
      ...options.headers,
    },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  const contentType = response.headers.get("content-type") ?? "";
  let payload = null;

  if (contentType.includes("application/json")) {
    payload = await response.json();
  } else if (contentType.length > 0) {
    payload = await response.text();
  }

  if (!response.ok) {
    return {
      success: false,
      code: payload?.error?.code ?? null,
      error: parseApiError(payload, `Request failed with status ${response.status}.`),
    };
  }

  return {
    success: true,
    data: payload,
  };
}

async function refreshScope(scope) {
  const scopeValue = normalizeScope(scope);
  const encodedScope = encodeURIComponent(scopeValue);
  return callTaxonomyApi(`/api/v1/categories?scope=${encodedScope}`);
}

function validateMutableScope(scope) {
  const normalizedScope = normalizeScope(scope);

  if (!MUTABLE_SCOPES.has(normalizedScope)) {
    return {
      success: false,
      error: "This scope is read-only in web settings. Use the operator workflow for platform taxonomy changes.",
    };
  }

  return { success: true, scope: normalizedScope };
}

async function withScopeRefresh(scope, mutationResult, successMessage) {
  if (!mutationResult.success) {
    return mutationResult;
  }

  const refreshed = await refreshScope(scope);
  revalidatePath("/settings/categories");

  if (!refreshed.success) {
    return {
      success: true,
      message: successMessage,
      categories: null,
      warning: "Saved successfully, but the latest category list could not be refreshed.",
    };
  }

  return {
    success: true,
    message: successMessage,
    categories: refreshed.data,
  };
}

export async function createCategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const name = input?.name?.trim();
  if (!name) {
    return { success: false, error: "Category name is required." };
  }

  const result = await callTaxonomyApi("/api/v1/categories", {
    method: "POST",
    body: {
      name,
      scope: scopeResult.scope,
    },
  });

  return withScopeRefresh(scopeResult.scope, result, `Created category \"${name}\".`);
}

export async function renameCategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const categoryId = input?.categoryId;
  const name = input?.name?.trim();

  if (!categoryId) {
    return { success: false, error: "Category id is required." };
  }

  if (!name) {
    return { success: false, error: "Category name is required." };
  }

  const result = await callTaxonomyApi(`/api/v1/categories/${categoryId}`, {
    method: "PATCH",
    body: {
      name,
    },
  });

  return withScopeRefresh(scopeResult.scope, result, `Renamed category to \"${name}\".`);
}

export async function archiveCategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const categoryId = input?.categoryId;
  if (!categoryId) {
    return { success: false, error: "Category id is required." };
  }

  const allowLinkedTransactions = input?.allowLinkedTransactions !== false;

  const result = await callTaxonomyApi(
    `/api/v1/categories/${categoryId}?allowLinkedTransactions=${allowLinkedTransactions}`,
    {
      method: "DELETE",
    },
  );

  return withScopeRefresh(scopeResult.scope, result, "Archived category.");
}

export async function reorderCategoriesAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const categoryIds = Array.isArray(input?.categoryIds) ? input.categoryIds : [];
  if (categoryIds.length === 0) {
    return { success: false, error: "CategoryIds are required to reorder." };
  }

  const result = await callTaxonomyApi("/api/v1/categories/reorder", {
    method: "POST",
    body: {
      scope: scopeResult.scope,
      categoryIds,
      expectedLastModifiedAtUtc: input?.expectedLastModifiedAtUtc ?? null,
    },
  });

  return withScopeRefresh(scopeResult.scope, result, "Updated category ordering.");
}

export async function createSubcategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const categoryId = input?.categoryId;
  const name = input?.name?.trim();

  if (!categoryId) {
    return { success: false, error: "Category id is required." };
  }

  if (!name) {
    return { success: false, error: "Subcategory name is required." };
  }

  const result = await callTaxonomyApi("/api/v1/subcategories", {
    method: "POST",
    body: {
      categoryId,
      name,
      isBusinessExpense: input?.isBusinessExpense === true,
    },
  });

  return withScopeRefresh(scopeResult.scope, result, `Created subcategory \"${name}\".`);
}

export async function renameSubcategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const subcategoryId = input?.subcategoryId;
  const name = input?.name?.trim();
  const isBusinessExpense = input?.isBusinessExpense;

  if (!subcategoryId) {
    return { success: false, error: "Subcategory id is required." };
  }

  const body = {};
  if (name !== undefined) {
    if (!name) {
      return { success: false, error: "Subcategory name is required." };
    }
    body.name = name;
  }
  
  if (isBusinessExpense !== undefined) {
    body.isBusinessExpense = isBusinessExpense;
  }

  const result = await callTaxonomyApi(`/api/v1/subcategories/${subcategoryId}`, {
    method: "PATCH",
    body,
  });

  const successMessage = name
    ? `Updated subcategory to \"${name}\".`
    : "Updated subcategory settings.";

  return withScopeRefresh(scopeResult.scope, result, successMessage);
}

export async function archiveSubcategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const subcategoryId = input?.subcategoryId;
  if (!subcategoryId) {
    return { success: false, error: "Subcategory id is required." };
  }

  const allowLinkedTransactions = input?.allowLinkedTransactions !== false;

  const result = await callTaxonomyApi(
    `/api/v1/subcategories/${subcategoryId}?allowLinkedTransactions=${allowLinkedTransactions}`,
    {
      method: "DELETE",
    },
  );

  return withScopeRefresh(scopeResult.scope, result, "Archived subcategory.");
}

export async function reparentSubcategoryAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const subcategoryId = input?.subcategoryId;
  const targetCategoryId = input?.targetCategoryId;

  if (!subcategoryId) {
    return { success: false, error: "Subcategory id is required." };
  }

  if (!targetCategoryId) {
    return { success: false, error: "Target category is required." };
  }

  const result = await callTaxonomyApi(`/api/v1/subcategories/${subcategoryId}/reparent`, {
    method: "POST",
    body: {
      targetCategoryId,
    },
  });

  return withScopeRefresh(scopeResult.scope, result, "Moved subcategory to new parent.");
}

export async function reorderSubcategoriesAction(input) {
  const scopeResult = validateMutableScope(input?.scope);
  if (!scopeResult.success) {
    return scopeResult;
  }

  const subcategoryIds = Array.isArray(input?.subcategoryIds) ? input.subcategoryIds : [];
  if (subcategoryIds.length === 0) {
    return { success: false, error: "SubcategoryIds are required to reorder." };
  }

  // Fallback pattern since API lacks bulk subcategory reorder
  let lastResult = null;
  for (let i = 0; i < subcategoryIds.length; i++) {
    const subcategoryId = subcategoryIds[i];
    lastResult = await callTaxonomyApi(`/api/v1/subcategories/${subcategoryId}`, {
      method: "PATCH",
      body: {
        displayOrder: i,
      },
    });
    // Break on first failure to avoid mangled state
    if (!lastResult.success) break;
  }

  return withScopeRefresh(scopeResult.scope, lastResult ?? { success: true }, "Updated subcategory ordering.");
}

