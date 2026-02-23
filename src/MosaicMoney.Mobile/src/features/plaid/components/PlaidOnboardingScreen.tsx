import { Pressable, SafeAreaView, StyleSheet, Text, View } from "react-native";
import { usePlaidLinkOnboarding } from "../hooks/usePlaidLinkOnboarding";

function ActionButton({
  label,
  onPress,
  disabled,
  tone = "primary",
}: {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  tone?: "primary" | "secondary";
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={disabled}
      style={({ pressed }) => [
        styles.button,
        tone === "secondary" ? styles.buttonSecondary : styles.buttonPrimary,
        disabled && styles.buttonDisabled,
        pressed && !disabled && styles.buttonPressed,
      ]}
    >
      <Text style={[styles.buttonText, tone === "secondary" ? styles.buttonTextSecondary : undefined]}>{label}</Text>
    </Pressable>
  );
}

export function PlaidOnboardingScreen() {
  const {
    status,
    error,
    linkSessionId,
    exchangeResult,
    isBusy,
    canStart,
    canOpen,
    start,
    openLink,
    reset,
  } = usePlaidLinkOnboarding();

  return (
    <SafeAreaView style={styles.page}>
      <View style={styles.card}>
        <Text style={styles.title}>Connect Your Bank</Text>
        <Text style={styles.body}>
          Securely connect your institution with Plaid Link. Tokens are issued and exchanged by Mosaic Money backend APIs.
        </Text>

        <View style={styles.stateCard}>
          <Text style={styles.stateLabel}>Current Status</Text>
          <Text style={styles.stateValue}>{status}</Text>
          {linkSessionId ? <Text style={styles.metaText}>Link session: {linkSessionId}</Text> : null}
        </View>

        {error ? (
          <View style={styles.errorCard}>
            <Text style={styles.errorTitle}>Connection issue</Text>
            <Text style={styles.errorBody}>{error}</Text>
          </View>
        ) : null}

        {exchangeResult ? (
          <View style={styles.successCard}>
            <Text style={styles.successTitle}>Bank linked</Text>
            <Text style={styles.successBody}>Item Id: {exchangeResult.itemId}</Text>
            <Text style={styles.successBody}>Credential Status: {exchangeResult.status}</Text>
            <Text style={styles.successBody}>Environment: {exchangeResult.environment}</Text>
          </View>
        ) : null}

        <View style={styles.actionsRow}>
          <ActionButton
            label={status === "issuingToken" ? "Preparing secure session..." : "Prepare Link Session"}
            onPress={() => {
              void start();
            }}
            disabled={!canStart || isBusy}
          />
          <ActionButton
            label={status === "openingLink" ? "Opening Plaid Link..." : "Open Plaid Link"}
            onPress={() => {
              void openLink();
            }}
            disabled={!canOpen || isBusy}
            tone="secondary"
          />
        </View>

        <ActionButton
          label="Reset"
          onPress={reset}
          disabled={isBusy}
          tone="secondary"
        />

        <Text style={styles.footnote}>
          Secrets never live on device. The mobile client only forwards short-lived public tokens to backend exchange endpoints.
        </Text>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: {
    backgroundColor: "#f2f4f7",
    flex: 1,
    padding: 16,
  },
  card: {
    backgroundColor: "#ffffff",
    borderColor: "#d8dee8",
    borderRadius: 12,
    borderWidth: 1,
    padding: 16,
  },
  title: {
    color: "#101828",
    fontSize: 24,
    fontWeight: "800",
  },
  body: {
    color: "#475467",
    fontSize: 14,
    lineHeight: 20,
    marginTop: 8,
  },
  stateCard: {
    backgroundColor: "#f8fafc",
    borderColor: "#d8dee8",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 16,
    padding: 12,
  },
  stateLabel: {
    color: "#475467",
    fontSize: 12,
    fontWeight: "700",
    textTransform: "uppercase",
  },
  stateValue: {
    color: "#101828",
    fontSize: 16,
    fontWeight: "700",
    marginTop: 6,
  },
  metaText: {
    color: "#667085",
    fontSize: 12,
    marginTop: 4,
  },
  errorCard: {
    backgroundColor: "#fef3f2",
    borderColor: "#fecdca",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 16,
    padding: 12,
  },
  errorTitle: {
    color: "#b42318",
    fontSize: 14,
    fontWeight: "700",
  },
  errorBody: {
    color: "#912018",
    fontSize: 13,
    marginTop: 6,
  },
  successCard: {
    backgroundColor: "#ecfdf3",
    borderColor: "#abefc6",
    borderRadius: 10,
    borderWidth: 1,
    marginTop: 16,
    padding: 12,
  },
  successTitle: {
    color: "#067647",
    fontSize: 14,
    fontWeight: "700",
  },
  successBody: {
    color: "#05603a",
    fontSize: 13,
    marginTop: 4,
  },
  actionsRow: {
    marginTop: 16,
    rowGap: 10,
  },
  button: {
    alignItems: "center",
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 12,
  },
  buttonPrimary: {
    backgroundColor: "#1849a9",
  },
  buttonSecondary: {
    backgroundColor: "#ffffff",
    borderColor: "#d0d5dd",
    borderWidth: 1,
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  buttonPressed: {
    opacity: 0.85,
  },
  buttonText: {
    color: "#ffffff",
    fontSize: 14,
    fontWeight: "700",
  },
  buttonTextSecondary: {
    color: "#344054",
  },
  footnote: {
    color: "#667085",
    fontSize: 12,
    lineHeight: 18,
    marginTop: 16,
  },
});
