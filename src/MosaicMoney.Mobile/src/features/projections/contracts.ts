import type {
  TransactionProjectionMetadataDto as SharedTransactionProjectionMetadataDto,
} from "../../../../../packages/shared/src/contracts";

export type TransactionProjectionMetadataDto = SharedTransactionProjectionMetadataDto;

export interface ProjectionQueryOptions {
  accountId?: string;
  fromDate?: string;
  toDate?: string;
  reviewStatus?: string;
  needsReviewOnly?: boolean;
  page?: number;
  pageSize?: number;
}
