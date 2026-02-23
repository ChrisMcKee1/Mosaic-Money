import { Stack } from "expo-router";

export default function RootLayout() {
  return (
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
        name="transactions/[transactionId]"
        options={{
          title: "Transaction Detail",
          headerLargeTitle: false,
        }}
      />
    </Stack>
  );
}
