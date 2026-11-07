import api from "@/lib/api";
import type {
  SurveyListParams,
  SurveyListResponse,
  SurveyResponse,
  CreateSurveyRequest,
  SurveyTimelineEvent,
  SurveyMediaUploadRequest,
  SurveyMediaResponse,
  UpdateSurveyChecklistRequest,
  SurveyChecklistResultResponse,
  SurveySyncLogResponse,
  SurveyDriveConnectionStatusResponse,
  SurveySiteConditionRequest,
  SurveySiteConditionResponse,
  SurveySiteConditionImportResponse,
} from "./types/surveys";
import type {
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
} from "./types/operationalProjects";

export const surveysApi = {
  listSurveys: (params: SurveyListParams = {}) => {
    const q = new URLSearchParams();
    if (params.constructionTypeCode) q.append("constructionTypeCode", params.constructionTypeCode);
    if (params.surveyorUserId != null) q.append("surveyorUserId", String(params.surveyorUserId));
    if (params.linkedProjectId != null) q.append("linkedProjectId", String(params.linkedProjectId));
    if (params.driveSyncStatus) q.append("driveSyncStatus", params.driveSyncStatus);
    if (params.dateFrom) q.append("dateFrom", params.dateFrom);
    if (params.dateTo) q.append("dateTo", params.dateTo);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<SurveyListResponse>(`/surveys${qs ? `?${qs}` : ""}`);
  },
  getSurvey: (id: number) => api.get<SurveyResponse>(`/surveys/${id}`),
  createSurvey: (body: CreateSurveyRequest) => api.post<SurveyResponse>("/surveys", body),
  updateSurvey: (id: number, body: CreateSurveyRequest) =>
    api.put<SurveyResponse>(`/surveys/${id}`, body),
  getSurveyDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/surveys/${id}/deletion-impact`),
  deleteSurvey: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult>(`/surveys/${id}`, { data: body }),
  getSurveyTimeline: (id: number, limit = 100) =>
    api.get<SurveyTimelineEvent[]>(`/surveys/${id}/timeline`, { params: { limit } }),
  uploadSurveyMedia: (id: number, request: SurveyMediaUploadRequest) => {
    const form = new FormData();
    form.append("file", request.file);
    if (request.note) form.append("note", request.note);
    if (request.capturedAt) form.append("capturedAt", request.capturedAt);
    if (request.latitude != null) form.append("latitude", String(request.latitude));
    if (request.longitude != null) form.append("longitude", String(request.longitude));
    return api.post<SurveyMediaResponse>(`/surveys/${id}/media`, form);
  },
  getSurveyMediaContent: (surveyId: number, mediaId: number) =>
    api.get<Blob>(`/surveys/${surveyId}/media/${mediaId}/content`, { responseType: "blob" }),
  deleteSurveyMedia: (surveyId: number, mediaId: number) =>
    api.delete(`/surveys/${surveyId}/media/${mediaId}`),
  retrySurveyMediaSync: (surveyId: number, mediaId: number) =>
    api.post<SurveyMediaResponse>(`/surveys/${surveyId}/media/${mediaId}/retry-sync`),
  updateSurveyChecklist: (
    surveyId: number,
    resultId: number,
    request: UpdateSurveyChecklistRequest,
  ) =>
    api.put<SurveyChecklistResultResponse>(`/surveys/${surveyId}/checklist/${resultId}`, request),
  getSurveySyncLog: (id: number) => api.get<SurveySyncLogResponse[]>(`/surveys/${id}/sync-log`),
  getSurveyDriveConnection: () =>
    api.get<SurveyDriveConnectionStatusResponse>("/surveys/drive-connection"),
  exportSurveyPdf: (id: number, lang: "vi" | "en" | "zh" | "ja") =>
    api.get<Blob>(`/surveys/${id}/export.pdf`, { params: { lang }, responseType: "blob" }),
  downloadSurveyConditionsTemplate: () =>
    api.get<Blob>("/surveys/conditions/template.csv", { responseType: "blob" }),
  replaceSurveyConditions: (id: number, conditions: SurveySiteConditionRequest[]) =>
    api.put<SurveySiteConditionResponse[]>(`/surveys/${id}/conditions`, { conditions }),
  importSurveyConditions: (id: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<SurveySiteConditionImportResponse>(`/surveys/${id}/conditions/import`, form);
  },
};
