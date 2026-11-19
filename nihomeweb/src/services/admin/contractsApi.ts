import axios from "axios";
import api from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import { uploadInChunks } from "@/lib/chunkedUpload";
import { uploadTracker } from "@/lib/uploadTracker";
import type { LegalChecklistTemplate } from "../legalChecklistTypes";
import type { SaveLegalChecklistTemplate } from "../legalChecklistTypes";
import type {
  ContractListParams,
  ContractListResponse,
  ContractDirection,
  ContractFilterOptionsResponse,
  ContractClassificationOptions,
  ContractResponse,
  ContractSourceLeadResponse,
  ContractExecutionProgressResponse,
  UpsertContractRequest,
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
  PaymentMilestoneStatus,
  UpdateContractPaymentScheduleRequest,
} from "./types/operationalProjects";
import type { DesignProjectResponse } from "./types/designProjects";
import type { ContractStatus } from "./types/contracts";
import type {
  LegalActivationPreview,
  ActivateContractWithLegalRequest,
  LegalActivationResult,
} from "./types/permits";
import type {
  ContractAppendixResponse,
  UpsertContractAppendixRequest,
  AppendixUploadResponse,
  ContractAttachmentResponse,
  ContractAttachmentKind,
  ContractTimelineEvent,
} from "./types/contractDocuments";
import { postIdempotent, managedFileName } from "./helpers";

/** Bytes the server holds when it refused a chunk for starting elsewhere (409), else null. */
const receivedBytesOf = (error: unknown) => {
  if (!axios.isAxiosError(error) || error.response?.status !== 409) return null;
  const received = (error.response.data as { receivedBytes?: unknown } | undefined)?.receivedBytes;
  return typeof received === "number" ? received : null;
};

/** The request never got an answer, or the server asked to retry shortly. */
const isConnectionLost = (error: unknown) =>
  axios.isAxiosError(error) &&
  error.code !== "ERR_CANCELED" &&
  (!error.response || [502, 503, 504].includes(error.response.status));

/** Waits before retrying a chunk; sooner when the browser comes back online. */
const waitToRetry = (ms: number, signal: AbortSignal) =>
  new Promise<void>((resolve, reject) => {
    const finish = (settle: () => void) => () => {
      window.clearTimeout(timer);
      window.removeEventListener("online", done);
      signal.removeEventListener("abort", aborted);
      settle();
    };
    const done = finish(resolve);
    const aborted = finish(() => reject(signal.reason));
    const timer = window.setTimeout(done, ms);
    window.addEventListener("online", done);
    signal.addEventListener("abort", aborted);
  });

/**
 * Sends a contract file in chunks so a dropped connection continues where it
 * stopped (files up to 5 GB); `complete` then stores it. Shows in the upload tray.
 */
const uploadContractFile = <T>(
  contractId: number,
  file: File,
  complete: (uploadId: string, signal: AbortSignal) => Promise<T>,
) => {
  const uploads = `/contracts/${contractId}/uploads`;
  return uploadInChunks(
    file,
    uploadTracker,
    {
      start: async (signal) =>
        (
          await api.post<{ uploadId: string; chunkSize: number }>(
            uploads,
            { fileName: file.name, size: file.size, contentType: file.type || undefined },
            { signal },
          )
        ).data,
      send: async (uploadId, offset, chunk, onProgress, signal) =>
        (
          await api.put<{ receivedBytes: number }>(`${uploads}/${uploadId}`, chunk, {
            params: { offset },
            headers: { "Content-Type": "application/octet-stream" },
            signal,
            onUploadProgress: (event) => onProgress(event.loaded),
          })
        ).data.receivedBytes,
      complete,
      cancel: (uploadId) => api.delete(`${uploads}/${uploadId}`),
    },
    { isConnectionLost, receivedBytesOf, wait: waitToRetry },
  );
};

export const contractsApi = {
  listContracts: (params?: ContractListParams) =>
    api.get<ContractListResponse>("/contracts", { params }),
  exportContracts: (params?: ContractListParams) =>
    api.get<ContractListResponse>("/contracts/export-data", { params }),
  getContractFilterOptions: (direction?: ContractDirection) =>
    api.get<ContractFilterOptionsResponse>("/contracts/filter-options", { params: { direction } }),
  getContractClassificationOptions: (direction?: ContractDirection) =>
    api.get<ContractClassificationOptions>("/contracts/classification-options", {
      params: { direction },
    }),
  getContract: (id: number, direction?: ContractDirection) =>
    api.get<ContractResponse>(`/contracts/${id}`, { params: { direction } }),
  previewNextContractNumber: () => api.get<{ contractNumber: string }>("/contracts/next-number"),
  getContractSourceLeads: (id: number, signal?: AbortSignal) =>
    api.get<ContractSourceLeadResponse[]>(`/contracts/${id}/source-leads`, { signal }),
  getContractExecutionProgress: (id: number, signal?: AbortSignal) =>
    api.get<ContractExecutionProgressResponse>(`/contracts/${id}/execution-progress`, { signal }),
  getContractContinuations: (id: number) =>
    api.get<{
      kinds: string[];
      contracts: {
        id: number;
        contractNumber: string;
        status: string;
        continuationKind: string | null;
        continuationReason: string | null;
        isSource: boolean;
      }[];
      opportunities: {
        id: number;
        name: string;
        stage: string;
        continuationKind: string;
        continuationReason: string;
        lostNote: string | null;
      }[];
    }>(`/contracts/${id}/continuations`),
  createContract: (body: UpsertContractRequest, key?: string) =>
    api.post<ContractResponse>("/contracts", body, withIdempotencyKey(key)),
  updateContract: (id: number, body: UpsertContractRequest) =>
    api.put<ContractResponse>(`/contracts/${id}`, body),
  getContractDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/contracts/${id}/deletion-impact`),
  deleteContract: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult>(`/contracts/${id}`, { data: body }),
  ensureContractDesignProject: (contractId: number) =>
    api.post<DesignProjectResponse>(`/contracts/${contractId}/design-project`, {}),
  linkContractQuote: (id: number, quoteId: number, rowVersion: string) =>
    postIdempotent<ContractResponse>(`/contracts/${id}/link-quote`, { quoteId, rowVersion }),
  transitionContract: (id: number, newStatus: ContractStatus, rowVersion?: string) =>
    api.post<ContractResponse>(`/contracts/${id}/transition`, { newStatus, rowVersion }),
  listLegalChecklistTemplates: () =>
    api.get<LegalChecklistTemplate[]>("/legal-checklist-templates"),
  saveLegalChecklistTemplate: (id: number | null, body: SaveLegalChecklistTemplate) =>
    id
      ? api.put<LegalChecklistTemplate>(`/legal-checklist-templates/${id}`, body)
      : postIdempotent<LegalChecklistTemplate>("/legal-checklist-templates", body),
  decideLegalChecklistTemplate: (
    id: number,
    action: "publish" | "retire" | "versions",
    body: { rowVersion: string; approvalReference?: string },
  ) => postIdempotent<LegalChecklistTemplate>(`/legal-checklist-templates/${id}/${action}`, body),
  getContractLegalActivationPreview: (id: number) =>
    api.get<LegalActivationPreview>(`/contracts/${id}/legal-activation-preview`),
  activateContractWithLegal: (id: number, body: ActivateContractWithLegalRequest) =>
    postIdempotent<LegalActivationResult>(`/contracts/${id}/activate-with-legal`, body),
  getProjectLegalSetupPreview: (id: number) =>
    api.get<LegalActivationPreview>(`/operational-projects/${id}/legal-setup`),
  setupProjectLegal: (id: number, body: Omit<ActivateContractWithLegalRequest, "rowVersion">) =>
    postIdempotent<LegalActivationResult>(`/operational-projects/${id}/legal-setup`, body),
  updateMilestoneStatus: (
    contractId: number,
    milestoneId: number,
    status: PaymentMilestoneStatus,
    actualPaymentDate?: string | null,
    responsibleAccountantUserId?: number | null,
    note?: string | null,
    rowVersion?: string,
  ) =>
    api.patch<ContractResponse>(`/contracts/${contractId}/milestones/${milestoneId}/status`, {
      status,
      actualPaymentDate,
      responsibleAccountantUserId,
      note,
      rowVersion,
    }),
  updateContractPaymentSchedule: (contractId: number, body: UpdateContractPaymentScheduleRequest) =>
    api.put<ContractResponse>(`/contracts/${contractId}/milestones`, body),
  listContractAppendices: (contractId: number) =>
    api.get<ContractAppendixResponse[]>(`/contracts/${contractId}/appendices`),
  createContractAppendix: (contractId: number, body: UpsertContractAppendixRequest) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices`, body),
  updateContractAppendix: (contractId: number, voId: number, body: UpsertContractAppendixRequest) =>
    api.put<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}`, body),
  submitContractAppendix: (contractId: number, voId: number) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}/submit`),
  approveContractAppendix: (contractId: number, voId: number, note?: string) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}/approve`, {
      note,
    }),
  rejectContractAppendix: (contractId: number, voId: number, note: string) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}/reject`, {
      note,
    }),
  deleteContractAppendix: (contractId: number, voId: number) =>
    api.delete(`/contracts/${contractId}/appendices/${voId}`),
  uploadContractAppendixFile: async (contractId: number, file: File) => ({
    data: await uploadContractFile(contractId, file, async (uploadId, signal) => {
      const url = `/contracts/${contractId}/uploads/${uploadId}/appendix-file`;
      return (await api.post<AppendixUploadResponse>(url, undefined, { signal })).data;
    }),
  }),
  listContractAttachments: (contractId: number) =>
    api.get<ContractAttachmentResponse[]>(`/contracts/${contractId}/attachments`),
  getContractAttachmentContent: (contractId: number, attachmentId: number) =>
    api.get<Blob>(`/contracts/${contractId}/attachments/${attachmentId}/content`, {
      responseType: "blob",
    }),
  getContractAppendixContent: (contractId: number, path: string) =>
    api.get<Blob>(`/contracts/${contractId}/appendices/files/${managedFileName(path)}/content`, {
      responseType: "blob",
    }),
  uploadContractAttachment: async (
    contractId: number,
    file: File,
    kind: ContractAttachmentKind,
    label?: string,
  ) => ({
    data: await uploadContractFile(contractId, file, async (uploadId, signal) => {
      const url = `/contracts/${contractId}/uploads/${uploadId}/attachment`;
      return (await api.post<ContractAttachmentResponse>(url, { kind, label }, { signal })).data;
    }),
  }),
  deleteContractAttachment: (contractId: number, attachmentId: number) =>
    api.delete(`/contracts/${contractId}/attachments/${attachmentId}`),
  getContractTimeline: (contractId: number, limit = 100) =>
    api.get<ContractTimelineEvent[]>(`/contracts/${contractId}/timeline`, {
      params: { limit },
    }),
  getVendorDocumentContent: (vendorId: number) =>
    api.get<Blob>(`/vendors/${vendorId}/capability-file/content`, { responseType: "blob" }),
  getAcceptanceDocumentContent: (recordId: number, path: string) =>
    api.get<Blob>(`/acceptance-records/${recordId}/documents/${managedFileName(path)}/content`, {
      responseType: "blob",
    }),
  getAsBuiltDocumentContent: (documentId: number) =>
    api.get<Blob>(`/as-built-documents/${documentId}/content`, { responseType: "blob" }),
  getHandoverDocumentContent: (recordId: number, path: string) =>
    api.get<Blob>(`/handover-records/${recordId}/documents/${managedFileName(path)}/content`, {
      responseType: "blob",
    }),
  getPermitDocumentContent: (permitId: number, path: string) =>
    api.get<Blob>(`/permits/${permitId}/documents/${managedFileName(path)}/content`, {
      responseType: "blob",
    }),
};
