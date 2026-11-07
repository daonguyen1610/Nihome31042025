export type BasicDesignDocStatus =
  | "InProgress"
  | "SubmittedForReview"
  | "InternallyApproved"
  | "SubmittedForPermit"
  | "PermitApproved"
  | "Rejected";

export const BASIC_DESIGN_DOC_STATUSES: BasicDesignDocStatus[] = [
  "InProgress",
  "SubmittedForReview",
  "InternallyApproved",
  "SubmittedForPermit",
  "PermitApproved",
  "Rejected",
];

export interface BasicDesignDocResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  disciplineCode: string;
  disciplineLabel?: string | null;
  documentCode: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  status: BasicDesignDocStatus;
  note?: string | null;
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface BasicDesignReadiness {
  requiredDisciplineCodes: string[];
  internallyApprovedDisciplineCodes: string[];
  readyForShopDrawing: boolean;
  clientApprovedDisciplineCodes: string[];
  hasClientApprovedReview: boolean;
  readyForDetailDesign: boolean;
}

export interface BasicDesignDocListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: BasicDesignDocResponse[];
  readiness: BasicDesignReadiness;
}

export interface BasicDesignDocListParams {
  designProjectId?: number;
  disciplineCode?: string;
  status?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateBasicDesignDocRequest {
  designProjectId: number;
  disciplineCode: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  note?: string | null;
}

export interface UpdateBasicDesignDocRequest {
  disciplineCode: string;
  title: string;
  description?: string | null;
  ownerUserId?: number | null;
  note?: string | null;
}

export interface TransitionBasicDesignDocStatusRequest {
  status: BasicDesignDocStatus;
}
