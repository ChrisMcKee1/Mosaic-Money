import { Pressable, StyleSheet, Text, View } from "react-native";

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
    backgroundColor: "#ffffff",
    borderColor: "#d5d9e0",
    borderRadius: 12,
    borderWidth: 1,
    marginHorizontal: 16,
    marginTop: 20,
    paddingHorizontal: 20,
    paddingVertical: 24,
  },
  title: {
    color: "#101828",
    fontSize: 18,
    fontWeight: "700",
    marginBottom: 8,
    textAlign: "center",
  },
  body: {
    color: "#344054",
    fontSize: 14,
    lineHeight: 20,
    textAlign: "center",
  },
  action: {
    backgroundColor: "#1849a9",
    borderRadius: 8,
    marginTop: 16,
    paddingHorizontal: 16,
    paddingVertical: 10,
  },
  actionPressed: {
    opacity: 0.82,
  },
  actionDisabled: {
    backgroundColor: "#98a2b3",
  },
  actionText: {
    color: "#ffffff",
    fontSize: 14,
    fontWeight: "600",
  },
});
