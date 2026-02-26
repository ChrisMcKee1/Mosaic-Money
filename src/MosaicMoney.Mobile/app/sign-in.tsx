import React from "react";
import { View, Text, StyleSheet, TouchableOpacity } from "react-native";
import { useOAuth } from "@clerk/clerk-expo";
import * as WebBrowser from "expo-web-browser";
import { useWarmUpBrowser } from "../src/auth/useWarmUpBrowser";
import { theme } from "../src/theme/tokens";

WebBrowser.maybeCompleteAuthSession();

export default function SignInScreen() {
  useWarmUpBrowser();

  const { startOAuthFlow } = useOAuth({ strategy: "oauth_microsoft" });

  const onPress = React.useCallback(async () => {
    try {
      const { createdSessionId, setActive } = await startOAuthFlow();

      if (createdSessionId && setActive) {
        setActive({ session: createdSessionId });
      } else {
        // Use signIn or signUp for next steps such as MFA
      }
    } catch (err) {
      console.error("OAuth error", err);
    }
  }, [startOAuthFlow]);

  return (
    <View style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.title}>Mosaic Money</Text>
        <Text style={styles.subtitle}>Sign in to manage your household finances</Text>
        
        <TouchableOpacity style={styles.button} onPress={onPress}>
          <Text style={styles.buttonText}>Sign in with Microsoft</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: theme.colors.background,
    justifyContent: "center",
    padding: theme.spacing.lg,
  },
  content: {
    alignItems: "center",
  },
  title: {
    fontSize: 32,
    fontWeight: "bold",
    color: theme.colors.textMain,
    marginBottom: theme.spacing.sm,
  },
  subtitle: {
    fontSize: 16,
    color: theme.colors.textMuted,
    marginBottom: theme.spacing.xl,
    textAlign: "center",
  },
  button: {
    backgroundColor: theme.colors.primary,
    paddingVertical: theme.spacing.md,
    paddingHorizontal: theme.spacing.xl,
    borderRadius: theme.borderRadius.md,
    width: "100%",
    alignItems: "center",
  },
  buttonText: {
    color: theme.colors.surface,
    fontSize: 16,
    fontWeight: "600",
  },
});
