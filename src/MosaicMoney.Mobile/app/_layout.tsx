import { Stack } from "expo-router";
import { useReviewMutationRecovery } from "../src/features/transactions/hooks/useReviewMutationRecovery";
import { theme } from "../src/theme/tokens";

function ReviewMutationRecoveryHost() {
  useReviewMutationRecovery();
  return null;
}

export default function RootLayout() {
  return (
    <>
      <ReviewMutationRecoveryHost />

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
          name="onboarding/plaid"
          options={{
            title: "Connect Bank",
            headerLargeTitle: false,
          }}
        />
      </Stack>
    </>
  );
}
