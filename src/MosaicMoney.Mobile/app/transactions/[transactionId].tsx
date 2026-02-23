import { useLocalSearchParams } from "expo-router";
import { TransactionDetailScreen } from "../../src/features/transactions/components/TransactionDetailScreen";

function normalizeRouteParam(value: string | string[] | undefined): string | undefined {
  if (Array.isArray(value)) {
    return value[0];
  }

  return value;
}

export default function TransactionDetailRoute() {
  const params = useLocalSearchParams<{ transactionId?: string | string[] }>();
  const transactionId = normalizeRouteParam(params.transactionId);

  return <TransactionDetailScreen transactionId={transactionId} />;
}
