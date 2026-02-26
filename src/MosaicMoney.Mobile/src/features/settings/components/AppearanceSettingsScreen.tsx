import { SafeAreaView, StyleSheet, Text, View } from "react-native";
import { theme } from "../../../theme/tokens";

export function AppearanceSettingsScreen() {
  return (
    <SafeAreaView style={styles.page}>
      <View style={styles.content}>
        <Text style={styles.heading}>Appearance</Text>
        <Text style={styles.subheading}>
          Mosaic Money mobile uses a consistent token-based visual system for readability and financial clarity.
        </Text>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Current Theme</Text>
          <Text style={styles.cardDescription}>
            Colors and typography are currently defined by shared app theme tokens. Full user-selectable theme variants are planned in a later UX slice.
          </Text>

          <View style={styles.swatchRow}>
            <View style={[styles.swatch, { backgroundColor: theme.colors.primary }]} />
            <View style={[styles.swatch, { backgroundColor: theme.colors.surface }]} />
            <View style={[styles.swatch, { backgroundColor: theme.colors.warning }]} />
            <View style={[styles.swatch, { backgroundColor: theme.colors.positive }]} />
          </View>
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
    marginBottom: 6,
  },
  cardDescription: {
    color: theme.colors.textMuted,
    fontSize: 14,
    lineHeight: 20,
  },
  swatchRow: {
    flexDirection: "row",
    gap: 10,
    marginTop: 14,
  },
  swatch: {
    borderColor: theme.colors.border,
    borderRadius: 999,
    borderWidth: 1,
    height: 28,
    width: 28,
  },
});
