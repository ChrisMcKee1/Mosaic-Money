import { Pressable, SafeAreaView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "@clerk/clerk-expo";
import { theme } from "../../../theme/tokens";

const isClerkConfigured = !!process.env.EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY;

export function SecuritySettingsScreen() {
  if (!isClerkConfigured) {
    return (
      <SafeAreaView style={styles.page}>
        <View style={styles.content}>
          <Text style={styles.heading}>Security & Authentication</Text>
          <Text style={styles.subheading}>Authentication is currently disabled in this environment.</Text>
        </View>
      </SafeAreaView>
    );
  }

  return <ConfiguredSecuritySettingsScreen />;
}

function ConfiguredSecuritySettingsScreen() {
  const router = useRouter();
  const { isSignedIn, signOut } = useAuth();

  return (
    <SafeAreaView style={styles.page}>
      <View style={styles.content}>
        <Text style={styles.heading}>Security & Authentication</Text>
        <Text style={styles.subheading}>Manage your session and account access controls.</Text>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Session Status</Text>
          <Text style={styles.cardDescription}>{isSignedIn ? "Signed in" : "Signed out"}</Text>

          {isSignedIn ? (
            <Pressable
              style={({ pressed }) => [styles.actionButton, pressed && styles.actionButtonPressed]}
              onPress={() => {
                void signOut();
              }}
            >
              <Text style={styles.actionButtonText}>Sign Out</Text>
            </Pressable>
          ) : (
            <Pressable
              style={({ pressed }) => [styles.actionButton, pressed && styles.actionButtonPressed]}
              onPress={() => router.replace("/sign-in")}
            >
              <Text style={styles.actionButtonText}>Go to Sign In</Text>
            </Pressable>
          )}
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  content: {
    padding: 16,
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
    marginBottom: 12,
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    padding: 16,
  },
  cardTitle: {
    color: theme.colors.textMain,
    fontSize: 18,
    fontWeight: "700",
    marginBottom: 4,
  },
  cardDescription: {
    color: theme.colors.textMuted,
    fontSize: 14,
  },
  actionButton: {
    alignSelf: "flex-start",
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: 8,
    borderWidth: 1,
    marginTop: 12,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  actionButtonPressed: {
    borderColor: theme.colors.primary,
  },
  actionButtonText: {
    color: theme.colors.primary,
    fontSize: 13,
    fontWeight: "700",
  },
});
