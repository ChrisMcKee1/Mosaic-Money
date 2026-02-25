import type {
  ApiErrorResponse as SharedApiErrorResponse,
  ReviewActionRequest as SharedReviewActionRequest,
  TransactionDto as SharedTransactionDto,
  CategorySearchResultDto as SharedCategorySearchResultDto,
} from "../../../../../packages/shared/src/contracts";

// TODO(mm-mob-03/mm-mob-04): Switch to @mosaic-money/shared package imports once mobile package linking is configured.
export type TransactionDto = SharedTransactionDto;
export type ApiErrorResponse = SharedApiErrorResponse;
export type ReviewActionRequest = SharedReviewActionRequest;
export type CategorySearchResultDto = SharedCategorySearchResultDto;

export type TransactionReviewStatus = "None" | "NeedsReview" | "Reviewed" | (string & {});
