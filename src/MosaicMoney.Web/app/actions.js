"use server";

import { fetchApi } from "../lib/api";

export async function searchCategories(query, limit = 10) {
  try {
    const normalizedQuery = query.trim();
    if (!normalizedQuery) return [];
    
    const data = await fetchApi(`/api/v1/search/categories?query=${encodeURIComponent(normalizedQuery)}&limit=${limit}`);
    return data || [];
  } catch (error) {
    console.error("Category search failed:", error);
    return [];
  }
}

export async function searchTransactions(query, limit = 20) {
  try {
    const normalizedQuery = query.trim();
    if (!normalizedQuery) return [];
    
    const data = await fetchApi(`/api/v1/search/transactions?query=${encodeURIComponent(normalizedQuery)}&limit=${limit}`);
    return data || [];
  } catch (error) {
    console.error("Transaction search failed:", error);
    return [];
  }
}