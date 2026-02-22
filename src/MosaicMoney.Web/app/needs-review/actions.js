"use server";

import { fetchApi } from "../../lib/api";
import { revalidatePath } from "next/cache";

export async function reviewTransaction(transactionId, action, data = {}) {
  try {
    const payload = {
      transactionId,
      action,
      ...data,
    };

    await fetchApi("/api/v1/review-actions", {
      method: "POST",
      body: JSON.stringify(payload),
    });

    revalidatePath("/needs-review");
    revalidatePath("/transactions");
    return { success: true };
  } catch (error) {
    console.error("Failed to review transaction:", error);
    return { success: false, error: error.message };
  }
}
