export interface SaveLegalChecklistTemplate {
  code: string;
  nameVi: string;
  name: string;
  nameZh: string;
  nameJa: string;
  scopeVi: string;
  scope: string;
  scopeZh: string;
  scopeJa: string;
  items: { permitTypeCode: string; isRequired: boolean }[];
  rowVersion?: string;
}

export interface LegalChecklistTemplate extends SaveLegalChecklistTemplate {
  id: number;
  version: number;
  status: "Draft" | "Published" | "Retired";
  createdByUserId: number;
  updatedByUserId: number;
  publishedByUserId?: number;
  publishedAt?: string;
  approvalReference?: string;
  rowVersion: string;
}
