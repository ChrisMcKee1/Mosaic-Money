"use server";

import { revalidatePath } from "next/cache";
import { fetchApi } from "../../../lib/api";

export async function inviteMember(prevState, formData) {
  const email = formData.get("email");
  const role = formData.get("role");
  const householdId = formData.get("householdId");

  if (!email || !email.includes("@")) {
    return { error: "Please provide a valid email address." };
  }

  if (!householdId) {
    return { error: "Select a household before sending invites." };
  }

  try {
    await fetchApi(`/api/v1/households/${householdId}/invites`, {
      method: "POST",
      body: JSON.stringify({ email, role }),
    });

    revalidatePath("/settings/household");
    return { success: true, message: `Invitation sent to ${email}` };
  } catch (error) {
    console.error("Failed to invite member:", error);
    return { error: "Failed to send invitation. Check the household context and try again." };
  }
}

export async function acceptInvite(householdId, inviteId, displayName) {
  if (!householdId || !inviteId) {
    return { error: "Missing household or invite context." };
  }

  try {
    await fetchApi(`/api/v1/households/${householdId}/invites/${inviteId}/accept`, {
      method: "POST",
      body: JSON.stringify({ displayName: displayName ?? null }),
    });

    revalidatePath("/settings/household");
    return { success: true };
  } catch (error) {
    console.error("Failed to accept invite:", error);
    return { error: "Failed to accept invitation." };
  }
}

export async function removeMember(householdId, memberId) {
  if (!householdId || !memberId) {
    return { error: "Missing household or member context." };
  }

  try {
    await fetchApi(`/api/v1/households/${householdId}/members/${memberId}`, {
      method: "DELETE",
    });

    revalidatePath("/settings/household");
    return { success: true };
  } catch (error) {
    console.error("Failed to remove member:", error);
    return { error: "Failed to remove member. Ensure at least one active member remains." };
  }
}

export async function cancelInvite(householdId, inviteId) {
  if (!householdId || !inviteId) {
    return { error: "Missing household or invite context." };
  }

  try {
    await fetchApi(`/api/v1/households/${householdId}/invites/${inviteId}`, {
      method: "DELETE",
    });

    revalidatePath("/settings/household");
    return { success: true };
  } catch (error) {
    console.error("Failed to cancel invite:", error);
    return { error: "Failed to cancel invitation." };
  }
}
