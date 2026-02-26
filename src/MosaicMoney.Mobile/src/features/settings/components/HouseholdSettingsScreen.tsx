import { useState } from "react";
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Pressable,
  RefreshControl,
  SafeAreaView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { useHouseholdSettings } from "../hooks/useHouseholdSettings";
import { theme } from "../../../theme/tokens";
import type { AccountSharingPreset } from "../../../../../../packages/shared/src/contracts";

export function HouseholdSettingsScreen() {
  const {
    household,
    members,
    invites,
    accountAccess,
    isLoading,
    error,
    refresh,
    inviteMember,
    acceptInvite,
    removeMember,
    cancelInvite,
    updateAccountSharingPreset,
  } = useHouseholdSettings();

  const [email, setEmail] = useState("");
  const [role, setRole] = useState("Member");
  const [isInviting, setIsInviting] = useState(false);
  const [updatingAccountId, setUpdatingAccountId] = useState<string | null>(null);

  const handleInvite = async () => {
    if (!email || !email.includes("@")) {
      Alert.alert("Invalid Email", "Please provide a valid email address.");
      return;
    }

    setIsInviting(true);
    const result = await inviteMember(email, role);
    setIsInviting(false);

    if (result.error) {
      Alert.alert("Error", result.error);
    } else {
      setEmail("");
      Alert.alert("Success", `Invitation sent to ${email}`);
    }
  };

  const handleAcceptInvite = (inviteId: string) => {
    Alert.prompt(
      "Accept Invite",
      "Enter your display name (optional):",
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Accept",
          onPress: async (displayName?: string) => {
            const result = await acceptInvite(inviteId, displayName);
            if (result.error) {
              Alert.alert("Error", result.error);
            }
          },
        },
      ],
      "plain-text"
    );
  };

  const handleRemoveMember = (memberId: string, displayName: string) => {
    Alert.alert(
      "Remove Member",
      `Are you sure you want to remove ${displayName}?`,
      [
        { text: "Cancel", style: "cancel" },
        {
          text: "Remove",
          style: "destructive",
          onPress: async () => {
            const result = await removeMember(memberId);
            if (result.error) {
              Alert.alert("Error", result.error);
            }
          },
        },
      ]
    );
  };

  const handleCancelInvite = (inviteId: string, email: string) => {
    Alert.alert(
      "Cancel Invite",
      `Are you sure you want to cancel the invite for ${email}?`,
      [
        { text: "No", style: "cancel" },
        {
          text: "Yes, Cancel",
          style: "destructive",
          onPress: async () => {
            const result = await cancelInvite(inviteId);
            if (result.error) {
              Alert.alert("Error", result.error);
            }
          },
        },
      ]
    );
  };

  const handleUpdateSharingPreset = async (accountId: string, preset: AccountSharingPreset) => {
    setUpdatingAccountId(accountId);
    const result = await updateAccountSharingPreset(accountId, preset);
    setUpdatingAccountId(null);

    if (result.error) {
      Alert.alert("Error", result.error);
    }
  };

  if (isLoading && !household) {
    return (
      <SafeAreaView style={styles.centeredPage}>
        <ActivityIndicator size="large" color={theme.colors.primary} />
        <Text style={styles.loadingText}>Loading household...</Text>
      </SafeAreaView>
    );
  }

  if (error && !household) {
    return (
      <SafeAreaView style={styles.centeredPage}>
        <Text style={styles.errorText}>{error}</Text>
        <Pressable style={styles.button} onPress={() => void refresh()}>
          <Text style={styles.buttonText}>Try Again</Text>
        </Pressable>
      </SafeAreaView>
    );
  }

  const renderContent = () => (
    <View style={styles.content}>
      {!household ? (
        <View style={styles.emptyState}>
          <Text style={styles.emptyStateText}>
            No household found yet. Create one through the API first.
          </Text>
        </View>
      ) : (
        <>
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Active Members</Text>
            {members.length === 0 ? (
              <Text style={styles.emptyText}>No active members.</Text>
            ) : (
              members.map((member) => (
                <View key={member.id} style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Text style={styles.cardTitle}>{member.displayName}</Text>
                    <Text style={styles.badge}>{member.role}</Text>
                  </View>
                  <Text style={styles.cardSubtitle}>Status: {member.membershipStatus}</Text>
                  <Pressable
                    style={styles.destructiveButton}
                    onPress={() => handleRemoveMember(member.id, member.displayName)}
                  >
                    <Text style={styles.destructiveButtonText}>Remove</Text>
                  </Pressable>
                </View>
              ))
            )}
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Pending Invites</Text>
            {invites.length === 0 ? (
              <Text style={styles.emptyText}>No pending invites.</Text>
            ) : (
              invites.map((invite) => (
                <View key={invite.id} style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Text style={styles.cardTitle}>{invite.email}</Text>
                    <Text style={styles.badge}>{invite.role}</Text>
                  </View>
                  <Text style={styles.cardSubtitle}>Status: {invite.membershipStatus}</Text>
                  <View style={styles.actionRow}>
                    <Pressable
                      style={styles.primaryButton}
                      onPress={() => handleAcceptInvite(invite.id)}
                    >
                      <Text style={styles.primaryButtonText}>Accept (Test)</Text>
                    </Pressable>
                    <Pressable
                      style={styles.destructiveButton}
                      onPress={() => handleCancelInvite(invite.id, invite.email)}
                    >
                      <Text style={styles.destructiveButtonText}>Cancel</Text>
                    </Pressable>
                  </View>
                </View>
              ))
            )}
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Invite New Member</Text>
            <View style={styles.formCard}>
              <Text style={styles.label}>Email Address</Text>
              <TextInput
                style={styles.input}
                placeholder="colleague@example.com"
                placeholderTextColor={theme.colors.textSubtle}
                value={email}
                onChangeText={setEmail}
                keyboardType="email-address"
                autoCapitalize="none"
              />
              
              <Text style={styles.label}>Role</Text>
              <View style={styles.roleSelector}>
                <Pressable
                  style={[styles.roleOption, role === "Member" && styles.roleOptionSelected]}
                  onPress={() => setRole("Member")}
                >
                  <Text style={[styles.roleText, role === "Member" && styles.roleTextSelected]}>
                    Member
                  </Text>
                </Pressable>
                <Pressable
                  style={[styles.roleOption, role === "Admin" && styles.roleOptionSelected]}
                  onPress={() => setRole("Admin")}
                >
                  <Text style={[styles.roleText, role === "Admin" && styles.roleTextSelected]}>
                    Admin
                  </Text>
                </Pressable>
              </View>

              <Pressable
                style={[styles.button, isInviting && styles.buttonDisabled]}
                onPress={handleInvite}
                disabled={isInviting}
              >
                <Text style={styles.buttonText}>
                  {isInviting ? "Sending..." : "Send Invite"}
                </Text>
              </Pressable>
            </View>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>Account Sharing Controls</Text>
            {accountAccess.length === 0 ? (
              <Text style={styles.emptyText}>No account-sharing controls are available for your member access.</Text>
            ) : (
              accountAccess.map((entry) => {
                const isOwner = entry.currentMemberAccessRole === "Owner";
                const isUpdating = updatingAccountId === entry.accountId;

                return (
                  <View key={entry.accountId} style={styles.card}>
                    <View style={styles.cardHeader}>
                      <Text style={styles.cardTitle}>{entry.accountName}</Text>
                      <Text style={styles.badge}>{entry.currentMemberAccessRole}</Text>
                    </View>
                    <Text style={styles.cardSubtitle}>
                      {entry.institutionName ? `${entry.institutionName} • ` : ""}
                      Sharing: {entry.sharingPreset}
                    </Text>

                    <View style={styles.roleSelector}>
                      {(["Mine", "Joint", "Shared"] as const).map((preset) => (
                        <Pressable
                          key={preset}
                          style={[
                            styles.roleOption,
                            entry.sharingPreset === preset && styles.roleOptionSelected,
                            (!isOwner || isUpdating) && styles.roleOptionDisabled,
                          ]}
                          disabled={!isOwner || isUpdating}
                          onPress={() => {
                            void handleUpdateSharingPreset(entry.accountId, preset);
                          }}
                        >
                          <Text style={[styles.roleText, entry.sharingPreset === preset && styles.roleTextSelected]}>
                            {preset}
                          </Text>
                        </Pressable>
                      ))}
                    </View>

                    {!isOwner && (
                      <Text style={styles.hintText}>
                        Only an account owner can change sharing settings.
                      </Text>
                    )}
                  </View>
                );
              })
            )}
          </View>
        </>
      )}
    </View>
  );

  return (
    <SafeAreaView style={styles.page}>
      <FlatList
        data={[{ key: "content" }]}
        renderItem={renderContent}
        refreshControl={<RefreshControl refreshing={isLoading} onRefresh={() => void refresh()} />}
        ListHeaderComponent={
          <View style={styles.headerContainer}>
            <Text style={styles.heading}>Household</Text>
            <Text style={styles.subheading}>
              {household?.name
                ? `Manage members and permissions for ${household.name}.`
                : "Manage who has access to your household accounts."}
            </Text>
          </View>
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  centeredPage: {
    alignItems: "center",
    backgroundColor: theme.colors.background,
    flex: 1,
    justifyContent: "center",
    padding: 20,
  },
  loadingText: {
    color: theme.colors.textMuted,
    fontSize: 16,
    marginTop: 16,
  },
  errorText: {
    color: theme.colors.warning,
    fontSize: 16,
    marginBottom: 16,
    textAlign: "center",
  },
  headerContainer: {
    backgroundColor: theme.colors.surface,
    borderBottomColor: theme.colors.border,
    borderBottomWidth: 1,
    paddingBottom: 16,
    paddingHorizontal: 16,
    paddingTop: 16,
  },
  heading: {
    color: theme.colors.textMain,
    fontSize: 28,
    fontWeight: "800",
    marginBottom: 4,
  },
  subheading: {
    color: theme.colors.textMuted,
    fontSize: 15,
  },
  content: {
    padding: 16,
  },
  section: {
    marginBottom: 24,
  },
  sectionTitle: {
    color: theme.colors.textMain,
    fontSize: 18,
    fontWeight: "700",
    marginBottom: 12,
  },
  emptyState: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    padding: 16,
  },
  emptyStateText: {
    color: theme.colors.textMuted,
    fontSize: 14,
  },
  emptyText: {
    color: theme.colors.textMuted,
    fontSize: 14,
    fontStyle: "italic",
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    marginBottom: 12,
    padding: 16,
  },
  cardHeader: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 4,
  },
  cardTitle: {
    color: theme.colors.textMain,
    fontSize: 16,
    fontWeight: "600",
  },
  cardSubtitle: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginBottom: 12,
  },
  badge: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.sm,
    borderWidth: 1,
    color: theme.colors.textMuted,
    fontSize: 12,
    overflow: "hidden",
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  actionRow: {
    flexDirection: "row",
    gap: 8,
  },
  primaryButton: {
    alignItems: "center",
    backgroundColor: theme.colors.primary,
    borderRadius: theme.borderRadius.md,
    flex: 1,
    paddingVertical: 8,
  },
  primaryButtonText: {
    color: theme.colors.background,
    fontSize: 14,
    fontWeight: "600",
  },
  destructiveButton: {
    alignItems: "center",
    backgroundColor: theme.colors.warningBg,
    borderColor: theme.colors.warning,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    flex: 1,
    paddingVertical: 8,
  },
  destructiveButtonText: {
    color: theme.colors.warning,
    fontSize: 14,
    fontWeight: "600",
  },
  formCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    padding: 16,
  },
  label: {
    color: theme.colors.textMain,
    fontSize: 14,
    fontWeight: "600",
    marginBottom: 8,
  },
  input: {
    backgroundColor: theme.colors.background,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    color: theme.colors.textMain,
    fontSize: 16,
    marginBottom: 16,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  roleSelector: {
    flexDirection: "row",
    gap: 8,
    marginBottom: 20,
  },
  roleOption: {
    alignItems: "center",
    backgroundColor: theme.colors.background,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.md,
    borderWidth: 1,
    flex: 1,
    paddingVertical: 10,
  },
  roleOptionSelected: {
    backgroundColor: theme.colors.primaryHover + "20",
    borderColor: theme.colors.primary,
  },
  roleOptionDisabled: {
    opacity: 0.45,
  },
  roleText: {
    color: theme.colors.textMuted,
    fontSize: 14,
    fontWeight: "600",
  },
  roleTextSelected: {
    color: theme.colors.primary,
  },
  hintText: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 8,
  },
  button: {
    alignItems: "center",
    backgroundColor: theme.colors.primary,
    borderRadius: theme.borderRadius.md,
    paddingVertical: 12,
  },
  buttonDisabled: {
    opacity: 0.5,
  },
  buttonText: {
    color: theme.colors.background,
    fontSize: 16,
    fontWeight: "600",
  },
});
