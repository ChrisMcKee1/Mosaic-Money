import { parseTransactionProjectionMetadataList } from "../../../../../../packages/shared/src/validation";
import { requestJson } from "../../../shared/services/mobileApiClient";
import type { ProjectionQueryOptions, TransactionProjectionMetadataDto } from "../contracts";

function buildProjectionQuery(options: ProjectionQueryOptions): string {
  const params = new URLSearchParams();

  if (options.accountId) {
    params.set("accountId", options.accountId);
  }

  if (options.fromDate) {
    params.set("fromDate", options.fromDate);
  }

  if (options.toDate) {
    params.set("toDate", options.toDate);
  }

  if (options.reviewStatus) {
    params.set("reviewStatus", options.reviewStatus);
  }

  if (options.needsReviewOnly) {
    params.set("needsReviewOnly", "true");
  }

  params.set("page", String(options.page ?? 1));
  params.set("pageSize", String(options.pageSize ?? 100));

  const query = params.toString();
  return query ? `?${query}` : "";
}

export async function fetchProjectionMetadata(
  options: ProjectionQueryOptions,
  signal?: AbortSignal,
): Promise<TransactionProjectionMetadataDto[]> {
  const query = buildProjectionQuery(options);
  return requestJson<TransactionProjectionMetadataDto[]>(`/api/v1/transactions/projection-metadata${query}`, {
    signal,
    parse: parseTransactionProjectionMetadataList,
  });
}
