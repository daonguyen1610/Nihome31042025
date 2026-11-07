import api from "@/lib/api";
import type {
  MasterDataOption,
  MasterDataCategory,
  UpsertMasterDataOptionRequest,
} from "./types/leads";
import type { WorkflowConfig, UpsertWorkflowConfigRequest } from "./types/workflows";

export const configurationApi = {
  getMasterDataOptions: (category: string, lang = "vi") =>
    api.get<MasterDataOption[]>(`/master-data/${encodeURIComponent(category)}`, {
      params: { lang },
    }),
  listMasterDataCategories: () => api.get<MasterDataCategory[]>("/master-data/categories"),
  listMasterDataByCategory: (category: string, includeInactive = false) =>
    api.get<MasterDataOption[]>(`/master-data/${encodeURIComponent(category)}`, {
      params: { includeInactive },
    }),
  createMasterDataOption: (category: string, body: UpsertMasterDataOptionRequest) =>
    api.post<MasterDataOption>(`/master-data/${encodeURIComponent(category)}`, body),
  updateMasterDataOption: (id: number, body: UpsertMasterDataOptionRequest) =>
    api.put<MasterDataOption>(`/master-data/options/${id}`, body),
  deleteMasterDataOption: (id: number) => api.delete(`/master-data/options/${id}`),
  listWorkflows: (includeInactive = false) =>
    api.get<WorkflowConfig[]>("/workflows", { params: { includeInactive } }),
  getWorkflow: (id: number) => api.get<WorkflowConfig>(`/workflows/${id}`),
  createWorkflow: (body: UpsertWorkflowConfigRequest) =>
    api.post<WorkflowConfig>("/workflows", body),
  updateWorkflow: (id: number, body: UpsertWorkflowConfigRequest) =>
    api.put<WorkflowConfig>(`/workflows/${id}`, body),
  deleteWorkflow: (id: number) => api.delete(`/workflows/${id}`),
};
