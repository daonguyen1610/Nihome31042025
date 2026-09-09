import api from "@/lib/api";

export type RfqStatus = "Draft" | "Issued" | "UnderEvaluation" | "Awarded" | "Closed" | "Cancelled";
export interface RfqHeader {
  id: number; operationalProjectId: number; code: string; title: string; projectCode: string; projectName: string;
  customerName: string; sourceBoqRevisionId: number; boqRevision: number; status: RfqStatus; ownerUserId: number;
  ownerName: string; invitedCount: number; receivedCount: number; issuedAt: string | null; dueAt: string;
  updatedAt: string; overdue: boolean; rowVersion: string;
}
export interface RfqLine { id: number; projectBoqLineId: number; itemCode: string; description: string; unit: string; quantity: number; budgetUnitPrice: number; lowestUnitPrice: number | null }
export interface RfqVendor { id: number; name: string; type: "Supplier" | "SubContractor" | "Both"; isActive: boolean; portalEnabled?: boolean; invitationSentAt?: string | null; invitationDeliveryError?: string | null }
export interface RfqFile { id: number; bidId: number | null; name: string }
export interface RfqBid {
  id: number; vendorId: number; revision: number; leadTimeDays: number; paymentTerms: string; validUntil: string;
  note: string | null; submittedAt: string; submittedBy: string; withdrawnAt: string | null; isCurrent: boolean;
  isComplete: boolean; isEligible: boolean; isLowest: boolean; total: number;
  currency: string; exchangeRateToVnd: number; subtotal: number; freightAmount: number; discountPercent: number;
  discountAmount: number; vatPercent: number; totalOriginal: number; commercialScore: number | null;
  priceScore: number | null; leadTimeScore: number | null; vendorRatingScore: number | null;
  weightedScore: number | null; evaluationNote: string | null; submittedViaPortal: boolean;
  lines: { rfqLineId: number; unitPrice: number; amount: number; unitPriceVnd: number; amountVnd: number;
    priceScore: number | null; leadTimeScore: number | null; vendorRatingScore: number | null; weightedScore: number | null }[];
}
export interface RfqAwardLine { rfqLineId: number; quantity: number; unitPrice: number; amountOriginal: number; amountVnd: number; materialRequestAllocations: { materialRequestLineId: number; materialRequestCode: string; quantity: number }[] }
export interface RfqAward { id: number; bidId: number; vendorId: number; vendorName: string; contractId: number; contractNumber: string; currency: string; exchangeRateToVnd: number; originalValue: number; valueVnd: number; awardedAt: string; lines: RfqAwardLine[] }
export interface RfqMaterialRequestOption { lineId: number; materialRequestId: number; materialRequestCode: string; projectBoqLineId: number; requestedQuantity: number; alreadyAllocatedQuantity: number; remainingQuantity: number }
export interface RfqDetail {
  header: RfqHeader; currency: string; note: string | null; lines: RfqLine[]; vendors: RfqVendor[]; bids: RfqBid[];
  events: { action: string; actor: string; at: string; reason: string | null }[]; documents: RfqFile[];
  selectedBidId: number | null; contractId: number | null; contractNumber: string | null; awardedAt: string | null;
  awardedBy: string | null; awardReason: string | null; awardSnapshotJson: string | null;
  scoring: { priceWeight: number; leadTimeWeight: number; vendorRatingWeight: number; commercialWeight: number };
  awards: RfqAward[]; materialRequests: RfqMaterialRequestOption[];
}
export interface RfqReferences {
  revisions: { id: number; revision: number; currency: string; lines: { id: number; itemCode: string; description: string; unit: string; quantity: number }[] }[];
  vendors: RfqVendor[]; owners: { id: number; name: string }[]; documents: RfqFile[];
}
export interface RfqDraft { title: string; sourceBoqRevisionId: number; ownerUserId: number; dueAt: string; note: string; vendorIds: number[]; lines: { projectBoqLineId: number; quantity: number }[]; priceWeight: number; leadTimeWeight: number; vendorRatingWeight: number; commercialWeight: number; rowVersion?: string }
export interface RfqBidDraft { vendorId: number; leadTimeDays: number; paymentTerms: string; validUntil: string; note: string; lines: { rfqLineId: number; unitPrice: number }[]; documentIds: number[]; currency: string; exchangeRateToVnd: number; freightAmount: number; discountPercent: number; discountAmount: number; vatPercent: number; rowVersion: string }
export interface RfqBatchAwardDraft { reason: string; overrideReason?: string; rowVersion: string; lines: { bidId: number; rfqLineId: number; contractType: "Supply" | "Subcontract"; quantity: number; materialRequestAllocations: { materialRequestLineId: number; quantity: number }[] }[] }
export interface VendorPortalRfq { code: string; title: string; dueAt: string; vendorName: string; canSubmit: boolean; lines: { id: number; itemCode: string; description: string; unit: string; quantity: number }[]; documents: { id: number; name: string }[] }
export interface RfqFilter { search?: string; status?: string; ownerUserId?: number; dueFrom?: string; dueTo?: string; overdue?: boolean; sortBy?: string; sortDirection?: string; page?: number; pageSize?: number }
const base = (projectId: number) => `/operational-projects/${projectId}/procurement/rfqs`;
export const rfqApi = {
  upload: (projectId: number, file: File) => {
    const data = new FormData();
    data.append("file", file);
    return api.post<RfqFile>(`${base(projectId)}/documents/upload`, data, { headers: { "Idempotency-Key": crypto.randomUUID() } });
  },
  list: (projectId: number, params: RfqFilter) => api.get<{ total: number; page: number; pageSize: number; items: RfqHeader[] }>(base(projectId), { params }),
  references: (projectId: number) => api.get<RfqReferences>(`${base(projectId)}/references`),
  detail: (projectId: number, id: number) => api.get<RfqDetail>(`${base(projectId)}/${id}`),
  save: (projectId: number, id: number | null, draft: RfqDraft) => id
    ? api.put<RfqDetail>(`${base(projectId)}/${id}`, draft) : api.post<RfqDetail>(base(projectId), draft),
  transition: (projectId: number, id: number, action: string, rowVersion: string, reason?: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/${action}`, { rowVersion, reason }),
  bid: (projectId: number, id: number, draft: RfqBidDraft) => api.post<RfqDetail>(`${base(projectId)}/${id}/bids`, draft),
  withdraw: (projectId: number, id: number, bidId: number, rowVersion: string, reason: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/bids/${bidId}/withdraw`, { rowVersion, reason }),
  award: (projectId: number, id: number, bidId: number, contractType: string, reason: string, rowVersion: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/award`, { bidId, contractType, reason, rowVersion }),
  evaluateBid: (projectId: number, id: number, bidId: number, commercialScore: number, note: string, rowVersion: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/bids/evaluate`, { bidId, commercialScore, note, rowVersion }),
  batchAward: (projectId: number, id: number, draft: RfqBatchAwardDraft) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/award-batch`, draft),
  resendInvitations: (projectId: number, id: number, rowVersion: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/invitations/resend`, { rowVersion }),
  attach: (projectId: number, id: number, documentId: number, rowVersion: string) =>
    api.post<RfqDetail>(`${base(projectId)}/${id}/documents`, { documentId, rowVersion }),
  exportList: (projectId: number, params: RfqFilter) => api.get<Blob>(`${base(projectId)}/export`, { params, responseType: "blob" }),
  exportDetail: (projectId: number, id: number) => api.get<Blob>(`${base(projectId)}/${id}/export`, { responseType: "blob" }),
  download: (projectId: number, id: number, documentId: number) => api.get<Blob>(`${base(projectId)}/${id}/documents/${documentId}/download`, { responseType: "blob" }),
  portal: (token: string) => api.get<VendorPortalRfq>("/vendor-rfqs", { headers: { "X-RFQ-Portal-Token": token } }),
  portalBid: (token: string, draft: Omit<RfqBidDraft, "vendorId" | "documentIds" | "rowVersion">) =>
    api.post<VendorPortalRfq>("/vendor-rfqs/bids", draft, { headers: { "X-RFQ-Portal-Token": token } }),
  portalDocument: (token: string, documentId: number) => api.get<Blob>(`/vendor-rfqs/documents/${documentId}/download`, {
    headers: { "X-RFQ-Portal-Token": token }, responseType: "blob",
  }),
};

export function saveRfqDownload(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = name;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
