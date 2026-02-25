import { Pressable, StyleSheet, Text, View } from "react-native";
import type { TransactionDto } from "../contracts";
import { formatCurrency, formatLedgerDate } from "../utils/formatters";
import { theme } from "../../../theme/tokens";

interface NeedsReviewQueueItemProps {
  transaction: TransactionDto;
  onPress: (transactionId: string) => void;
}

export function NeedsReviewQueueItem({ transaction, onPress }: NeedsReviewQueueItemProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Open transaction details for ${transaction.description}`}
      onPress={() => onPress(transaction.id)}
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
    >
      <View style={styles.rowTop}>
        <Text numberOfLines={2} style={styles.description}>
          {transaction.description}
        </Text>
        <Text style={[styles.amount, transaction.amount < 0 ? styles.amountNegative : styles.amountPositive]}>
          {formatCurrency(transaction.amount)}
        </Text>
      </View>

      <View style={styles.rowMeta}>
        <Text style={styles.metaText}>{formatLedgerDate(transaction.transactionDate)}</Text>
        <View style={styles.pendingBadge}>
          <Text style={styles.pendingText}>Pending Review</Text>
        </View>
      </View>

      {transaction.reviewReason ? (
        <Text style={styles.reviewReason} numberOfLines={2}>
          Reason: {transaction.reviewReason}
        </Text>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 12,
    borderWidth: 1,
    marginBottom: 12,
    padding: 14,
  },
  cardPressed: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.primaryHover,
  },
  rowTop: {
    alignItems: "flex-start",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  description: {
    color: theme.colors.textMain,
    flex: 1,
    fontSize: 16,
    fontWeight: "600",
    marginRight: 12,
  },
  amount: {
    fontSize: 15,
    fontWeight: "700",
    fontFamily: "monospace",
  },
  amountNegative: {
    color: theme.colors.negative,
  },
  amountPositive: {
    color: theme.colors.positive,
  },
  rowMeta: {
    alignItems: "center",
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 10,
  },
  metaText: {
    color: theme.colors.textMuted,
    fontSize: 13,
  },
  pendingBadge: {
    backgroundColor: theme.colors.warningBg,
    borderRadius: 999,
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderWidth: 1,
    borderColor: theme.colors.warning,
  },
  pendingText: {
    color: theme.colors.warning,
    fontSize: 12,
    fontWeight: "700",
  },
  reviewReason: {
    color: theme.colors.warning,
    fontSize: 13,
    marginTop: 8,
  },
});
