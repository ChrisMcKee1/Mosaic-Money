import { Stack, useRouter, useSegments } from "expo-router";
import { ClerkProvider, ClerkLoaded, useAuth } from "@clerk/clerk-expo";
import { useEffect } from "react";
import { useReviewMutationRecovery } from "../src/features/transactions/hooks/useReviewMutationRecovery";
import { theme } from "../src/theme/tokens";
import { tokenCache } from "../src/auth/tokenCache";
import { setAuthTokenProvider } from "../src/shared/services/mobileApiClient";

const publishableKey = process.env.EXPO_PUBLIC_CLERK_PUBLISHABLE_KEY!;

function ReviewMutationRecoveryHost() {
  useReviewMutationRecovery();
  return null;
}

function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isLoaded, isSignedIn, getToken } = useAuth();
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    if (!isLoaded) return;

    const inAuthGroup = segments[0] === "sign-in";

    if (!isSignedIn && !inAuthGroup) {
      // Redirect to sign-in if not signed in and not already there
      router.replace("/sign-in");
    } else if (isSignedIn && inAuthGroup) {
      // Redirect to home if signed in and trying to access sign-in
      router.replace("/");
    }
  }, [isSignedIn, isLoaded, segments, router]);

  useEffect(() => {
    if (!isLoaded) {
      setAuthTokenProvider(null);
      return;
    }

    setAuthTokenProvider(async () => {
      if (!isSignedIn) {
        return null;
      }

      return await getToken();
    });

    return () => {
      setAuthTokenProvider(null);
    };
  }, [getToken, isLoaded, isSignedIn]);

  return <>{children}</>;
}

export default function RootLayout() {
  if (!publishableKey) {
    // Fallback for local dev without Clerk config
    return (
      <>
        <ReviewMutationRecoveryHost />
        <RootStack />
      </>
    );
  }

  return (
    <ClerkProvider tokenCache={tokenCache} publishableKey={publishableKey}>
      <ClerkLoaded>
        <ReviewMutationRecoveryHost />
        <AuthGuard>
          <RootStack />
        </AuthGuard>
      </ClerkLoaded>
    </ClerkProvider>
  );
}

function RootStack() {
  return (
    <Stack
      screenOptions={{
        contentStyle: { backgroundColor: theme.colors.background },
        headerStyle: { backgroundColor: theme.colors.surface },
        headerTintColor: theme.colors.textMain,
        headerBackTitle: "Back",
      }}
    >
      <Stack.Screen
        name="index"
        options={{
          title: "NeedsReview",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="sign-in"
        options={{
          title: "Sign In",
          headerShown: false,
        }}
      />
      <Stack.Screen
        name="dashboard"
        options={{
          title: "Dashboard",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="transactions/index"
        options={{
          title: "Transactions",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="accounts"
        options={{
          title: "Accounts",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="categories"
        options={{
          title: "Categories",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="investments"
        options={{
          title: "Investments",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="recurrings"
        options={{
          title: "Recurrings",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="transactions/[transactionId]"
        options={{
          title: "Transaction Detail",
          headerLargeTitle: false,
        }}
      />
      <Stack.Screen
        name="settings/index"
        options={{
          title: "Settings",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="settings/household"
        options={{
          title: "Household",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="settings/appearance"
        options={{
          title: "Appearance",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="settings/security"
        options={{
          title: "Security",
          headerLargeTitle: true,
        }}
      />
      <Stack.Screen
        name="onboarding/plaid"
        options={{
          title: "Connect Bank",
          headerLargeTitle: false,
        }}
      />
    </Stack>
  );
}
