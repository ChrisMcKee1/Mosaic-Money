import { SafeAreaView, ScrollView, StyleSheet, Text, View } from "react-native";
import { CartesianChart, Line } from "victory-native";
import { PrimarySurfaceNav } from "../../../shared/components/PrimarySurfaceNav";
import { theme } from "../../../theme/tokens";
import { formatCurrency } from "../../transactions/utils/formatters";

const mockAccounts = [
  { id: "1", name: "Fidelity Brokerage", type: "Brokerage", balance: 45230.5, change1W: 1250.2, change1WPercent: 2.8 },
  { id: "2", name: "Vanguard 401k", type: "Retirement", balance: 128450.0, change1W: -450.0, change1WPercent: -0.35 },
  { id: "3", name: "Coinbase", type: "Crypto", balance: 12450.8, change1W: 2100.5, change1WPercent: 20.3 },
  { id: "4", name: "Robinhood", type: "Crypto", balance: 3200.0, change1W: -150.0, change1WPercent: -4.5 },
];

const mockHistory = [
  { day: 1, value: 185000 },
  { day: 2, value: 186200 },
  { day: 3, value: 185800 },
  { day: 4, value: 187500 },
  { day: 5, value: 188100 },
  { day: 6, value: 187900 },
  { day: 7, value: 189331.3 },
];

export function InvestmentsOverviewScreen() {
  return (
    <SafeAreaView style={styles.page}>
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.heading}>Investments</Text>
        <Text style={styles.subheading}>Portfolio snapshot aligned to current web investment surface contract.</Text>
        <PrimarySurfaceNav />

        <View style={styles.chartContainer}>
          <CartesianChart
            data={mockHistory}
            xKey="day"
            yKeys={["value"]}
          >
            {({ points }) => (
              <Line
                points={points.value}
                color={theme.colors.primary}
                strokeWidth={3}
                animate={{ type: "timing", duration: 500 }}
              />
            )}
          </CartesianChart>
        </View>

        {mockAccounts.map((account) => (
          <View key={account.id} style={styles.card}>
            <Text style={styles.name}>{account.name}</Text>
            <Text style={styles.type}>{account.type}</Text>
            <Text style={styles.balance}>{formatCurrency(account.balance)}</Text>
            <Text style={[styles.change, account.change1W >= 0 ? styles.positive : styles.negative]}>
              {account.change1W >= 0 ? "+" : ""}
              {formatCurrency(account.change1W)} ({account.change1WPercent.toFixed(2)}%)
            </Text>
          </View>
        ))}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: theme.colors.background,
    flex: 1,
  },
  content: {
    paddingHorizontal: 16,
    paddingBottom: 24,
  },
  heading: {
    color: theme.colors.textMain,
    fontSize: 24,
    fontWeight: "800",
    letterSpacing: -0.5,
    marginTop: 8,
  },
  subheading: {
    color: theme.colors.textMuted,
    fontSize: 14,
    marginTop: 6,
  },
  chartContainer: {
    height: 200,
    marginTop: 16,
    marginBottom: 8,
  },
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderWidth: 1,
    borderRadius: theme.borderRadius.lg,
    marginTop: 10,
    padding: 14,
  },
  name: {
    color: theme.colors.textMain,
    fontSize: 15,
    fontWeight: "700",
  },
  type: {
    color: theme.colors.textMuted,
    fontSize: 12,
    marginTop: 4,
  },
  balance: {
    color: theme.colors.primary,
    fontFamily: theme.typography.mono.fontFamily,
    fontSize: 20,
    fontWeight: "800",
    marginTop: 10,
  },
  change: {
    fontSize: 12,
    fontWeight: "700",
    marginTop: 4,
  },
  positive: {
    color: theme.colors.positive,
  },
  negative: {
    color: theme.colors.negative,
  },
});
