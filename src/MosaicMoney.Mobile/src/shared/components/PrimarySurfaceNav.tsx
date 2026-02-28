import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";
import { usePathname, useRouter } from "expo-router";
import { theme } from "../../theme/tokens";

const primaryRoutes = [
  { label: "Needs Review", route: "/" },
  { label: "Dashboard", route: "/dashboard" },
  { label: "Assistant", route: "/assistant" },
  { label: "Transactions", route: "/transactions" },
  { label: "Accounts", route: "/accounts" },
  { label: "Categories", route: "/categories" },
  { label: "Investments", route: "/investments" },
  { label: "Recurrings", route: "/recurrings" },
  { label: "Settings", route: "/settings" },
];

export function PrimarySurfaceNav() {
  const pathname = usePathname();
  const router = useRouter();

  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scroll} contentContainerStyle={styles.container}>
      {primaryRoutes.map((item) => {
        const isActive = pathname === item.route;

        return (
          <Pressable
            key={item.route}
            accessibilityRole="button"
            accessibilityLabel={`Open ${item.label}`}
            onPress={() => router.push(item.route)}
            style={({ pressed }) => [
              styles.tab,
              isActive && styles.tabActive,
              pressed && styles.tabPressed,
            ]}
          >
            <Text style={[styles.tabText, isActive && styles.tabTextActive]}>{item.label}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  scroll: {
    marginTop: 12,
  },
  container: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: theme.borderRadius.lg,
    borderWidth: 1,
    columnGap: 6,
    flexDirection: "row",
    padding: 4,
  },
  tab: {
    alignItems: "center",
    borderRadius: theme.borderRadius.md,
    minWidth: 102,
    paddingHorizontal: 10,
    paddingVertical: 8,
  },
  tabActive: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.primary,
    borderWidth: 1,
  },
  tabPressed: {
    opacity: 0.85,
  },
  tabText: {
    color: theme.colors.textMuted,
    fontSize: 13,
    fontWeight: "700",
  },
  tabTextActive: {
    color: theme.colors.primary,
  },
});
