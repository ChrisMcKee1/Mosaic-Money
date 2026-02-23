import { Stack } from "expo-router";
import { useReviewMutationRecovery } from "../src/features/transactions/hooks/useReviewMutationRecovery";

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
          contentStyle: { backgroundColor: "#f2f4f7" },
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
