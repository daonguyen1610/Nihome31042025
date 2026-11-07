export type DrawingRevisionTargetType = "BasicDesignDoc" | "ShopDrawing";

export interface DrawingRevisionResponse {
  id: number;
  targetType: DrawingRevisionTargetType;
  targetId: number;
  targetCode?: string | null;
  targetTitle?: string | null;
  revisionNumber: number;
  revisionLabel: string;
  reasonCode: string;
  reasonLabel?: string | null;
  note: string;
  isCurrent: boolean;
  isSuperseded: boolean;
  createdAt: string;
  createdByUserId: number;
  createdByName?: string | null;
}

export interface DrawingRevisionListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: DrawingRevisionResponse[];
}

export interface DrawingRevisionListParams {
  designProjectId: number;
  targetType?: DrawingRevisionTargetType;
  targetId?: number;
  page?: number;
  pageSize?: number;
}

export interface CreateDrawingRevisionRequest {
  targetType: DrawingRevisionTargetType;
  targetId: number;
  reasonCode: string;
  note: string;
}

export interface DrawingRevisionDiffResponse {
  from: DrawingRevisionResponse;
  to: DrawingRevisionResponse;
  changes: string[];
}
