import { Pressable, StyleSheet, Text, View } from "react-native";
import type { TransactionProjectionMetadataDto } from "../contracts";
import { formatCurrency, formatLedgerDate } from "../../transactions/utils/formatters";
import { theme } from "../../../theme/tokens";

interface ProjectionListSectionProps {
  items: TransactionProjectionMetadataDto[];
  selectedProjectionId?: string;
  onSelect: (projectionId: string) => void;
}

function buildProjectionBadges(item: TransactionProjectionMetadataDto): string[] {
  const badges: string[] = [];

  if (item.excludeFromBudget) {
    badges.push("Business Excluded");
  }

  if (item.recurring.isLinked) {
    badges.push("Recurring Linked");
  }

  if (item.reimbursement.hasPendingHumanReview) {
    badges.push("Reimbursement Review");
  }

  if (item.isExtraPrincipal) {
    badges.push("Extra Principal");
  }

  return badges;
}

export function ProjectionListSection({
  items,
  selectedProjectionId,
  onSelect,
}: ProjectionListSectionProps) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Projection List</Text>
      <Text style={styles.sectionBody}>Tap any row to inspect read-only projection details.</Text>

      {items.map((item) => {
        const badges = buildProjectionBadges(item);
        const isSelected = item.id === selectedProjectionId;

        return (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            accessibilityLabel={`Inspect projection details for ${item.description}`}
            onPress={() => onSelect(item.id)}
            style={({ pressed }) => [
              styles.itemCard,
              isSelected && styles.itemCardSelected,
              pressed && styles.itemCardPressed,
            ]}
          >
            <View style={styles.itemTopRow}>
              <Text style={styles.itemDescription} numberOfLines={2}>
                {item.description}
              </Text>
              <Text style={[styles.itemAmount, item.rawAmount < 0 ? styles.itemAmountNegative : styles.itemAmountPositive]}>
                {formatCurrency(item.rawAmount)}
              </Text>
            </View>

            <View style={styles.itemMetaRow}>
              <Text style={styles.itemMeta}>{formatLedgerDate(item.rawTransactionDate)}</Text>
              <Text style={styles.itemMeta}>Review: {item.reviewStatus}</Text>
            </View>

            {badges.length > 0 ? (
              <View style={styles.badgeRow}>
                {badges.map((badge) => (
                  <View key={`${item.id}-${badge}`} style={styles.badge}>
                    <Text style={styles.badgeText}>{badge}</Text>
                  </View>
                ))}
              </View>
            ) : null}
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    marginTop: 12,
  },
  sectionTitle: {
    color: theme.colors.textMain,
    fontSize: 20,
    fontWeight: "800",
    letterSpacing: -0.5,
  },
  sectionBody: {
    color: theme.colors.textMuted,
    fontSize: 13,
    lineHeight: 20,
    marginBottom: 10,
    marginTop: 4,
  },
  itemCard: {
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 12,
    borderWidth: 1,
    marginBottom: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  itemCardSelected: {
    borderColor: theme.colors.primary,
    borderWidth: 2,
    paddingHorizontal: 11,
    paddingVertical: 11,
  },
  itemCardPressed: {
    backgroundColor: theme.colors.surfaceHover,
  },
  itemTopRow: {
    alignItems: "flex-start",
    flexDirection: "row",
    justifyContent: "space-between",
  },
  itemDescription: {
    color: theme.colors.textMain,
    flex: 1,
    fontSize: 15,
    fontWeight: "700",
    marginRight: 10,
  },
  itemAmount: {
    fontSize: 15,
    fontWeight: "800",
    fontFamily: "monospace",
  },
  itemAmountNegative: {
    color: theme.colors.negative,
  },
  itemAmountPositive: {
    color: theme.colors.positive,
  },
  itemMetaRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginTop: 8,
  },
  itemMeta: {
    color: theme.colors.textMuted,
    fontSize: 12,
  },
  badgeRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    marginTop: 8,
  },
  badge: {
    backgroundColor: theme.colors.surfaceHover,
    borderColor: theme.colors.border,
    borderRadius: 999,
    borderWidth: 1,
    marginRight: 6,
    marginTop: 6,
    paddingHorizontal: 8,
    paddingVertical: 3,
  },
  badgeText: {
    color: theme.colors.primary,
    fontSize: 11,
    fontWeight: "700",
  },
});
