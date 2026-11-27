export type ConceptOptionStatus =
  | "Drafting"
  | "PendingInternalReview"
  | "PresentedToClient"
  | "ClientRequestedChanges"
  | "Finalized"
  | "Discarded";

export const CONCEPT_OPTION_STATUSES: ConceptOptionStatus[] = [
  "Drafting",
  "PendingInternalReview",
  "PresentedToClient",
  "ClientRequestedChanges",
  "Finalized",
  "Discarded",
];

export interface ConceptOptionResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  name: string;
  description?: string | null;
  internalNote?: string | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  presentedAt?: string | null;
  status: ConceptOptionStatus;
  createdAt: string;
  updatedAt: string;
}

export type ConceptArtifactKind =
  "Model3D" | "Perspective" | "SiteRoofPlan" | "FloorPlan" | "Section" | "Presentation" | "Other";

export interface ConceptRevisionResponse {
  id: number;
  conceptOptionId: number;
  number: number;
  createdAt: string;
  frozenAt?: string | null;
  canCreateRevision: boolean;
  /** A `drawing_revision_reason` code; null only for R00. */
  reasonCode?: string | null;
  requiredKinds: ConceptArtifactKind[];
  /** What blocks the internal submission; only on the latest unsubmitted version. */
  readiness?: {
    missingKinds: ConceptArtifactKind[];
    needsVisual: boolean;
    filesWaiting: number;
    filesBlocked: number;
    hasUnansweredEarlierFeedback: boolean;
  } | null;
  artifacts: {
    id: number;
    projectDocumentId: number;
    fileName: string;
    kind: ConceptArtifactKind;
    floorLabel?: string | null;
    syncStatus: string;
  }[];
  waivers: { kind: ConceptArtifactKind; reason: string; createdAt: string }[];
  feedback: {
    id: number;
    projectDocumentId?: number | null;
    customerName: string;
    channel: string;
    evidenceSource?: string | null;
    occurredAt: string;
    comment: string;
    response?: string | null;
    outcome?: string | null;
    respondedAt?: string | null;
  }[];
}

export interface ConceptOptionListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ConceptOptionResponse[];
}

export interface ConceptOptionListParams {
  designProjectId?: number;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateConceptOptionRequest {
  designProjectId: number;
  name: string;
  description?: string | null;
  internalNote?: string | null;
  ownerUserId?: number | null;
  presentedAt?: string | null;
}

export interface UpdateConceptOptionRequest {
  name: string;
  description?: string | null;
  internalNote?: string | null;
  ownerUserId?: number | null;
  presentedAt?: string | null;
}

export interface TransitionConceptOptionStatusRequest {
  status: ConceptOptionStatus;
}
