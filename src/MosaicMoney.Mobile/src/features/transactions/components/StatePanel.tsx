import { Pressable, StyleSheet, Text, View } from "react-native";
import { theme } from "../../../theme/tokens";

interface StatePanelProps {
  title: string;
  body: string;
  actionLabel?: string;
  onAction?: () => void;
  disabled?: boolean;
}

export function StatePanel({ title, body, actionLabel, onAction, disabled }: StatePanelProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.body}>{body}</Text>
      {actionLabel && onAction ? (
        <Pressable
          accessibilityRole="button"
          onPress={onAction}
          disabled={disabled}
          style={({ pressed }) => [
            styles.action,
            disabled && styles.actionDisabled,
            pressed && !disabled && styles.actionPressed,
          ]}
        >
          <Text style={styles.actionText}>{actionLabel}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: "center",
    backgroundColor: theme.colors.surface,
    borderColor: theme.colors.border,
    borderRadius: 12,
    borderWidth: 1,
    marginHorizontal: 16,
    marginTop: 20,
    paddingHorizontal: 20,
    paddingVertical: 24,
  },
  title: {
    color: theme.colors.textMain,
    fontSize: 18,
    fontWeight: "700",
    marginBottom: 8,
    textAlign: "center",
    letterSpacing: -0.5,
  },
  body: {
    color: theme.colors.textMuted,
    fontSize: 14,
    lineHeight: 20,
    textAlign: "center",
  },
  action: {
    backgroundColor: theme.colors.primary,
    borderRadius: 8,
    marginTop: 16,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  actionPressed: {
    backgroundColor: theme.colors.primaryHover,
  },
  actionDisabled: {
    backgroundColor: theme.colors.surfaceHover,
  },
  actionText: {
    color: theme.colors.background,
    fontSize: 14,
    fontWeight: "600",
  },
});
