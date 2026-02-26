import { Pressable, SafeAreaView, StyleSheet, Text, View } from "react-native";
import { useRouter } from "expo-router";
import { theme } from "../../../theme/tokens";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";

export function SettingsScreen() {
  const router = useRouter();

  return (
    <SafeAreaView style={styles.page}>
      <View style={styles.headerContainer}>
        <Text style={styles.heading}>Settings</Text>
        <Text style={styles.subheading}>Configure your Mosaic Money experience.</Text>
        <PrimarySurfaceNav />
      </View>

      <View style={styles.content}>
        <Pressable
          style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
          onPress={() => router.push("/settings/household")}
        >
          <Text style={styles.cardTitle}>Household</Text>
          <Text style={styles.cardDescription}>
            Manage members, invites, and access to your household accounts.
          </Text>
        </Pressable>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
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
    marginBottom: 8,
  },
  content: {
    padding: 16,
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    padding: 16,
  },
  cardPressed: {
    backgroundColor: theme.colors.surfaceHover,
  },
  cardTitle: {
    color: theme.colors.textMain,
    fontSize: 18,
    fontWeight: "600",
    marginBottom: 4,
  },
  cardDescription: {
    color: theme.colors.textMuted,
    fontSize: 14,
  },
});
