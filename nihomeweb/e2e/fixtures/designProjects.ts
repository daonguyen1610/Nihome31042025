import type { APIRequestContext, APIResponse } from "@playwright/test";
import { TEST_USERS } from "./auth";

interface CreateDesignProjectOptions {
  headers: Record<string, string>;
  name: string;
  customerId: number;
  projectManagerUserId?: number;
  startDate?: string;
  deadline?: string;
}

export async function createDesignProject(
  api: APIRequestContext,
  options: CreateDesignProjectOptions,
): Promise<number> {
  const bearer = options.headers.Authorization?.replace(/^Bearer\s+/i, "");
  const payload = bearer
    ? (JSON.parse(Buffer.from(bearer.split(".")[1], "base64url").toString()) as Record<
        string,
        string
      >)
    : {};
  const authenticatedUserId = Number(
    payload.nameid ??
      payload.sub ??
      Object.entries(payload).find(([key]) => key.endsWith("/nameidentifier"))?.[1],
  );
  const designManagerUserId =
    options.projectManagerUserId ??
    (Number.isInteger(authenticatedUserId) && authenticatedUserId > 0
      ? authenticatedUserId
      : undefined);
  const operationalProjectResponse = await api.post("/api/operational-projects", {
    headers: options.headers,
    data: {
      name: `${options.name} operational project`,
      customerId: options.customerId,
    },
  });
  if (!operationalProjectResponse.ok()) {
    throw new Error(
      `Operational project creation failed (${operationalProjectResponse.status()}): ${await operationalProjectResponse.text()}`,
    );
  }
  const operationalProjectId = (await operationalProjectResponse.json()).id as number;
  let lastError = "";

  for (let attempt = 1; attempt <= 5; attempt += 1) {
    const response = await api.post("/api/design-projects", {
      headers: options.headers,
      data: {
        name: options.name,
        customerId: options.customerId,
        operationalProjectId,
        designManagerUserId,
        designLeadUserId: designManagerUserId,
        startDate: options.startDate ?? "2026-01-01",
        deadline: options.deadline ?? "2026-12-31",
      },
    });

    if (response.ok()) {
      return (await response.json()).id as number;
    }

    lastError = await response.text();
    if (response.status() !== 409) {
      throw new Error(`Design project creation failed (${response.status()}): ${lastError}`);
    }

    await new Promise((resolve) => setTimeout(resolve, attempt * 100));
  }

  throw new Error(`Design project creation still conflicted after 5 attempts: ${lastError}`);
}

interface ApproveDesignStageOptions {
  headers: Record<string, string>;
  projectId: number;
  stage: "Concept" | "BasicDesign" | "ShopDrawing";
  category: "DesignConcept" | "DesignBasic" | "DesignShopDrawing";
  sourceEntityType: "ConceptOption" | "BasicDesignDoc" | "ShopDrawing";
  sourceRecordIds: number[];
}

export async function isGoogleDriveConnected(
  api: APIRequestContext,
  headers: Record<string, string>,
): Promise<boolean> {
  const response = await api.get("/api/site-settings/google-drive/status", { headers });
  if (!response.ok()) {
    throw new Error(
      `Google Drive status check failed (${response.status()}): ${await response.text()}`,
    );
  }

  const status = (await response.json()) as { status?: string };
  return status.status === "Connected";
}

/**
 * Creates the real evidence required by the NICON design gate: an approved
 * phase task, synced source-bound Project Documents, internal circulation,
 * consolidation, client submission and a recorded ClientApproved decision.
 */
export async function approveDesignStage(
  api: APIRequestContext,
  options: ApproveDesignStageOptions,
): Promise<void> {
  const unique = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  const json = async <T>(response: APIResponse, label: string): Promise<T> => {
    const text = await response.text();
    if (!response.ok()) throw new Error(`${label} (${response.status()}): ${text}`);
    return (text ? JSON.parse(text) : {}) as T;
  };
  const login = async (user: (typeof TEST_USERS)[keyof typeof TEST_USERS]) => {
    const response = await api.post("/api/auth/login", {
      data: { phoneNumber: user.phoneNumber, password: user.password },
    });
    return (await json<{ accessToken: string }>(response, `login ${user.role}`)).accessToken;
  };

  const project = await json<{ operationalProjectId: number; designManagerUserId: number }>(
    await api.get(`/api/design-projects/${options.projectId}`, { headers: options.headers }),
    "load design project",
  );
  const users = await json<{ items: Array<{ id: number; phoneNumber: string }> }>(
    await api.get("/api/users?take=200", { headers: options.headers }),
    "load design users",
  );
  const architect = users.items.find(
    (user) => user.phoneNumber === TEST_USERS.architect.phoneNumber,
  );
  const recipient = users.items.find((user) => user.phoneNumber === TEST_USERS.sale.phoneNumber);
  if (!architect || !recipient) throw new Error("Required seeded design-review users are missing.");

  let team = await json<{ members: Array<{ id: number; userId: number }> }>(
    await api.get(`/api/operational-projects/${project.operationalProjectId}/team`, {
      headers: options.headers,
    }),
    "load project team",
  );
  let assignee = team.members.find((member) => member.userId === architect.id);
  if (!assignee) {
    assignee = await json<{ id: number; userId: number }>(
      await api.post(`/api/operational-projects/${project.operationalProjectId}/team/members`, {
        headers: { ...options.headers, "Idempotency-Key": `review-assignee-${unique}` },
        data: {
          userId: architect.id,
          position: "Kiến trúc sư triển khai hồ sơ",
          startedAt: "2026-01-01T00:00:00Z",
          roles: [{ roleCode: "Architect", scope: "Project" }],
        },
      }),
      "add review task assignee",
    );
  }
  team = await json<{ members: Array<{ id: number; userId: number }> }>(
    await api.get(`/api/operational-projects/${project.operationalProjectId}/team`, {
      headers: options.headers,
    }),
    "reload project team",
  );
  const approver = team.members.find((member) => member.userId === project.designManagerUserId);
  if (!approver) throw new Error("Design Manager/Lead project member is missing.");

  let schedule = await json<{
    phases: Array<{ id: number; code: string }>;
  }>(
    await api.get(
      `/api/operational-projects/${project.operationalProjectId}/design-schedule?pageSize=100`,
      {
        headers: options.headers,
      },
    ),
    "load design schedule",
  );
  if (schedule.phases.length === 0) {
    schedule = await json(
      await api.post(
        `/api/operational-projects/${project.operationalProjectId}/design-schedule/initialize`,
        {
          headers: { ...options.headers, "Idempotency-Key": `review-schedule-${unique}` },
          data: {
            phases: [
              { code: "Concept", weight: 34 },
              { code: "BasicDesign", weight: 33 },
              { code: "ShopDrawing", weight: 33 },
            ],
          },
        },
      ),
      "initialize design schedule",
    );
  }
  const phase = schedule.phases.find((item) => item.code === options.stage);
  if (!phase) throw new Error(`Design schedule phase ${options.stage} is missing.`);

  const task = await json<{ id: number; rowVersion: string }>(
    await api.post(
      `/api/operational-projects/${project.operationalProjectId}/design-schedule/phases/${phase.id}/tasks`,
      {
        headers: { ...options.headers, "Idempotency-Key": `review-task-${unique}` },
        data: {
          code: `REV-${options.stage}-${unique}`.slice(0, 80),
          name: `Hoàn thiện hồ sơ ${options.stage}`,
          description: "Đầu việc kiểm soát hồ sơ trước khi trình nội bộ.",
          departmentCode: "architecture",
          assigneeMemberId: assignee.id,
          approverMemberId: approver.id,
          isMilestone: false,
          plannedStart: "2026-01-01",
          plannedEnd: "2026-01-02",
          status: "NotStarted",
          progressPercent: 0,
          weight: 1,
          predecessorTaskIds: [],
          ccMemberIds: [],
          isCritical: true,
        },
      },
    ),
    "create approved-stage task",
  );
  const architectToken = await login(TEST_USERS.architect);
  const completed = await json<{ rowVersion: string }>(
    await api.post(
      `/api/operational-projects/${project.operationalProjectId}/design-schedule/tasks/${task.id}/complete`,
      {
        headers: {
          Authorization: `Bearer ${architectToken}`,
          "Idempotency-Key": `review-task-complete-${unique}`,
        },
        data: { rowVersion: task.rowVersion },
      },
    ),
    "complete design task",
  );
  await json(
    await api.post(
      `/api/operational-projects/${project.operationalProjectId}/design-schedule/tasks/${task.id}/review`,
      {
        headers: { ...options.headers, "Idempotency-Key": `review-task-approve-${unique}` },
        data: { decision: "Approve", rowVersion: completed.rowVersion },
      },
    ),
    "approve design task",
  );

  const documentIds: number[] = [];
  for (const sourceRecordId of options.sourceRecordIds) {
    const uploaded = await json<{ id: number }>(
      await api.post(`/api/operational-projects/${project.operationalProjectId}/documents`, {
        headers: {
          ...options.headers,
          "Idempotency-Key": `review-document-${sourceRecordId}-${unique}`,
        },
        multipart: {
          file: {
            name: `${options.sourceEntityType}-${sourceRecordId}.pdf`,
            mimeType: "application/pdf",
            buffer: Buffer.from(`%PDF-1.4 NICON ${options.stage} ${sourceRecordId}`),
          },
          category: options.category,
          sourceModule: "Design",
          sourceEntityType: options.sourceEntityType,
          sourceRecordId: String(sourceRecordId),
        },
      }),
      "upload design review evidence",
    );
    documentIds.push(uploaded.id);
  }

  let conceptRevisionId: number | undefined;
  let acceptanceDocumentId: number | undefined;
  if (options.stage === "Concept") {
    if (options.sourceRecordIds.length !== 1) {
      throw new Error("Concept review must select exactly one option.");
    }
    const optionId = options.sourceRecordIds[0];
    const revisions = await json<Array<{ id: number; number: number }>>(
      await api.get(`/api/concept-options/${optionId}/revisions`, { headers: options.headers }),
      "load Concept revisions",
    );
    conceptRevisionId = revisions[0]?.id;
    if (!conceptRevisionId) throw new Error("Concept R00 was not created.");
    const perspective = await json<{ id: number }>(
      await api.post(`/api/operational-projects/${project.operationalProjectId}/documents`, {
        headers: { ...options.headers, "Idempotency-Key": `review-perspective-${unique}` },
        multipart: {
          file: {
            name: `Concept-${optionId}-perspective.png`,
            mimeType: "image/png",
            buffer: Buffer.from(
              "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO5Wl1sAAAAASUVORK5CYII=",
              "base64",
            ),
          },
          category: options.category,
          sourceModule: "Design",
          sourceEntityType: "ConceptOption",
          sourceRecordId: String(optionId),
        },
      }),
      "upload Concept perspective",
    );
    documentIds.push(perspective.id);
    for (const [documentId, kind] of [
      [documentIds[0], "Presentation"],
      [perspective.id, "Perspective"],
    ] as const) {
      await json(
        await api.post(
          `/api/concept-options/${optionId}/revisions/${conceptRevisionId}/artifacts`,
          {
            headers: { ...options.headers, "Idempotency-Key": `review-artifact-${kind}-${unique}` },
            data: { projectDocumentId: documentId, kind },
          },
        ),
        `attach Concept ${kind}`,
      );
    }
    for (const kind of ["SiteRoofPlan", "FloorPlan", "Section"]) {
      await json(
        await api.post(`/api/concept-options/${optionId}/revisions/${conceptRevisionId}/waivers`, {
          headers: { ...options.headers, "Idempotency-Key": `review-waiver-${kind}-${unique}` },
          data: { kind, reason: "Không áp dụng trong hồ sơ thử nghiệm này." },
        }),
        `waive Concept ${kind}`,
      );
    }
    const acceptance = await json<{ id: number }>(
      await api.post(`/api/operational-projects/${project.operationalProjectId}/documents`, {
        headers: { ...options.headers, "Idempotency-Key": `review-acceptance-${unique}` },
        multipart: {
          file: {
            name: `TK-M03-Concept-${optionId}-signed.pdf`,
            mimeType: "application/pdf",
            buffer: Buffer.from(`%PDF-1.4 NICON signed Concept ${optionId}`),
          },
          category: options.category,
          sourceModule: "Design",
          sourceEntityType: "ConceptOption",
          sourceRecordId: String(optionId),
        },
      }),
      "upload signed Concept acceptance",
    );
    acceptanceDocumentId = acceptance.id;
  }

  for (let attempt = 0; attempt < 30; attempt += 1) {
    const documents = await json<Array<{ id: number; syncStatus: string }>>(
      await api.get(`/api/operational-projects/${project.operationalProjectId}/documents`, {
        headers: options.headers,
      }),
      "poll design review evidence",
    );
    if (
      [...documentIds, ...(acceptanceDocumentId ? [acceptanceDocumentId] : [])].every((id) =>
        documents.some((document) => document.id === id && document.syncStatus === "Synced"),
      )
    )
      break;
    if (attempt === 29) throw new Error("Design review evidence did not reach Synced state.");
    await new Promise((resolve) => setTimeout(resolve, 200));
  }

  let review = await json<{ id: number; rowVersion: string }>(
    await api.post(`/api/design-projects/${options.projectId}/reviews`, {
      headers: { ...options.headers, "Idempotency-Key": `review-create-${unique}` },
      data: {
        stage: options.stage,
        title: `Hồ sơ ${options.stage} trình khách`,
        recipientUserIds: [recipient.id],
        projectDocumentIds: documentIds,
        ...(conceptRevisionId ? { conceptRevisionId } : {}),
      },
    }),
    "create design review",
  );
  const advance = async (path: string, data: Record<string, unknown>, label: string) => {
    const response = await api.post(
      `/api/design-projects/${options.projectId}/reviews/${review.id}/${path}`,
      {
        headers: { ...options.headers, "Idempotency-Key": `${path}-${unique}` },
        data,
      },
    );
    review = await json(response, label);
  };
  await advance("submit-internal", { rowVersion: review.rowVersion }, "submit internal review");
  await advance(
    "consolidate",
    {
      internalSummary: "Các phòng ban đã thống nhất hồ sơ.",
      rowVersion: review.rowVersion,
    },
    "consolidate review",
  );
  await advance("submit-client", { rowVersion: review.rowVersion }, "submit review to client");
  await advance(
    "client-decision",
    {
      decision: "ClientApproved",
      note: "Khách hàng xác nhận hồ sơ.",
      ...(acceptanceDocumentId
        ? {
            clientAcceptanceDocumentId: acceptanceDocumentId,
            clientAcceptanceSignedBy: "Đại diện chủ đầu tư NICON E2E",
            clientAcceptanceSignedAt: new Date().toISOString(),
          }
        : {}),
      rowVersion: review.rowVersion,
    },
    "record client approval",
  );
}

/**
 * Creates a customer this spec alone owns.
 *
 * Specs used to take the newest customer from the list instead. Customers come
 * back newest first, so that picked up whatever another spec had just created,
 * and deleting a customer cascades through AggregateDeletionService to its
 * design projects — so the other spec's cleanup silently removed the project
 * this one was working on, and the failure surfaced far from its cause.
 */
export async function createOwnCustomer(
  api: APIRequestContext,
  headers: Record<string, string>,
  label: string,
): Promise<number> {
  const unique = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
  const response = await api.post("/api/customers", {
    headers,
    data: {
      name: `E2E ${label} customer ${unique}`,
      type: "Company",
      sourceCode: "referral",
      relationshipStatus: "InProgress",
      taxId: `04${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      address: "88 Lý Thường Kiệt, Hà Nội",
      representativeName: `${label} Rep ${unique}`,
      primaryContact: {
        fullName: `${label} Rep ${unique}`,
        phone: `098${Math.floor(1_000_000 + Math.random() * 8_999_999)}`,
        email: `e2e-${unique}@nihome.test`,
        isPrimary: true,
      },
    },
  });

  if (!response.ok()) {
    throw new Error(`Customer creation failed (${response.status()}): ${await response.text()}`);
  }

  return (await response.json()).id as number;
}
