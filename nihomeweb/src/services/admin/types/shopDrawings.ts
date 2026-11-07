export type ShopDrawingStatus =
  "Drafting" | "InReview" | "Approved" | "PendingIfc" | "Released" | "Rejected";

export const SHOP_DRAWING_STATUSES: ShopDrawingStatus[] = [
  "Drafting",
  "InReview",
  "Approved",
  "PendingIfc",
  "Released",
  "Rejected",
];

export interface ShopDrawingResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  disciplineCode: string;
  disciplineLabel?: string | null;
  constructionItem: string;
  drawingCode: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  status: ShopDrawingStatus;
  note?: string | null;
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ShopDrawingListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ShopDrawingResponse[];
  /** Per-status counts, keyed by `ShopDrawingStatus`. */
  statusCounts: Partial<Record<ShopDrawingStatus, number>>;
}

export interface ShopDrawingListParams {
  designProjectId?: number;
  disciplineCode?: string;
  constructionItem?: string;
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateShopDrawingRequest {
  designProjectId: number;
  disciplineCode: string;
  constructionItem: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  note?: string | null;
}

export interface UpdateShopDrawingRequest {
  disciplineCode: string;
  constructionItem: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  note?: string | null;
}

export interface TransitionShopDrawingStatusRequest {
  status: ShopDrawingStatus;
}

export interface BulkDeleteShopDrawingsRequest {
  ids: number[];
}

export interface ShopDrawingBulkDeleteFailure {
  id: number;
  message: string;
}

export interface ShopDrawingBulkDeleteResponse {
  requested: number;
  deleted: number;
  failures: ShopDrawingBulkDeleteFailure[];
}
