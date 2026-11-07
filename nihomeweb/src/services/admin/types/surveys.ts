import type { CsvImportError } from "./quotes";

export type SurveyDriveSyncStatus = "NotSynced" | "Syncing" | "Synced" | "Failed";

export const SURVEY_DRIVE_STATUSES: SurveyDriveSyncStatus[] = [
  "NotSynced",
  "Syncing",
  "Synced",
  "Failed",
];

export type SurveyMediaSyncStatus = "Pending" | "Processing" | "Synced" | "Failed";

export type SurveyChecklistStatus = "Ok" | "NeedsAttention" | "Failed";

export interface SurveyMediaResponse {
  id: number;
  originalFileName: string;
  contentType: string;
  extension: string;
  size: number;
  note?: string | null;
  capturedAt?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  syncStatus: SurveyMediaSyncStatus;
  syncAttemptCount: number;
  maxSyncAttempts: number;
  syncError?: string | null;
  nextSyncAttemptAt?: string | null;
  lastSyncAttemptAt?: string | null;
  syncedAt?: string | null;
  createdAt: string;
  contentUrl: string;
}

export interface SurveyChecklistResultResponse {
  id: number;
  templateCode: string;
  templateTitle: string;
  status?: SurveyChecklistStatus | null;
  note?: string | null;
  sortOrder: number;
  updatedAt: string;
}

export interface SurveySyncLogResponse {
  mediaId: number;
  fileName: string;
  status: SurveyMediaSyncStatus;
  attemptCount: number;
  maxAttempts: number;
  error?: string | null;
  lastAttemptAt?: string | null;
  nextAttemptAt?: string | null;
  syncedAt?: string | null;
}

export type SurveyDriveConnectionStatus =
  "Connected" | "ReadOnly" | "InvalidRoot" | "ReconnectRequired" | "Unavailable";

export interface SurveyDriveConnectionStatusResponse {
  status: SurveyDriveConnectionStatus;
  syncMode: "PushOnly";
  accountEmail?: string | null;
  storageType?: "MyDrive" | "SharedDrive" | null;
  rootFolderName?: string | null;
  rootFolderLink?: string | null;
  error?: string | null;
}

export interface SurveyMediaUploadRequest {
  file: File;
  note?: string;
  capturedAt?: string;
  latitude?: number;
  longitude?: number;
}

export interface UpdateSurveyChecklistRequest {
  status: SurveyChecklistStatus;
  note?: string | null;
  sortOrder: number;
}

export interface SurveyResponse {
  id: number;
  code: string;
  location: string;
  constructionTypeCode?: string | null;
  constructionTypeLabel?: string | null;
  surveyDate: string;
  surveyorUserId?: number | null;
  surveyorName?: string | null;
  linkedProjectId?: number | null;
  linkedProjectName?: string | null;
  linkedOpportunityId?: number | null;
  linkedOpportunityName?: string | null;
  operationalProjectId: number;
  operationalProjectName?: string | null;
  note?: string | null;
  driveSyncStatus: SurveyDriveSyncStatus;
  driveSyncError?: string | null;
  lastSyncedAt?: string | null;
  driveFolderLink?: string | null;
  media: SurveyMediaResponse[];
  checklistResults: SurveyChecklistResultResponse[];
  siteConditions: SurveySiteConditionResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface SurveyListItemResponse {
  id: number;
  code: string;
  location: string;
  constructionTypeCode?: string | null;
  constructionTypeLabel?: string | null;
  surveyDate: string;
  surveyorUserId?: number | null;
  surveyorName?: string | null;
  linkedProjectId?: number | null;
  linkedProjectName?: string | null;
  linkedOpportunityId?: number | null;
  linkedOpportunityName?: string | null;
  operationalProjectId: number;
  operationalProjectName?: string | null;
  driveSyncStatus: SurveyDriveSyncStatus;
  driveSyncError?: string | null;
  lastSyncedAt?: string | null;
  updatedAt: string;
}

export interface SurveyListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: SurveyListItemResponse[];
}

export interface SurveyListParams {
  constructionTypeCode?: string;
  surveyorUserId?: number;
  linkedProjectId?: number;
  /** Comma-separated `SurveyDriveSyncStatus` values, e.g. "Synced,Failed". */
  driveSyncStatus?: string;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateSurveyRequest {
  location: string;
  constructionTypeCode?: string | null;
  surveyDate: string;
  surveyorUserId?: number | null;
  linkedProjectId?: number | null;
  linkedOpportunityId?: number | null;
  operationalProjectId?: number | null;
  note?: string | null;
}

export type SurveyConditionCategory = "RightOfWay" | "Elevation" | "Infrastructure";

export type SurveyConditionStatus = "Unknown" | "Available" | "Unavailable" | "NeedsInvestigation";

export interface SurveySiteConditionRequest {
  category: SurveyConditionCategory;
  code: string;
  statusCode: SurveyConditionStatus;
  numericValue?: number | null;
  unitCode?: string | null;
  referenceCode?: string | null;
  description?: string | null;
  note?: string | null;
}

export interface SurveySiteConditionResponse extends SurveySiteConditionRequest {
  id: number;
  createdAt: string;
  updatedAt: string;
}

export interface SurveySiteConditionImportResponse {
  conditions: SurveySiteConditionResponse[];
  errors: CsvImportError[];
}

export interface SurveyTimelineEvent {
  id: number;
  occurredAt: string;
  action: string;
  message?: string | null;
  userId?: number | null;
  userName?: string | null;
}
