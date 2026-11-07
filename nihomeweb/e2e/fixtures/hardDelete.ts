import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type APIResponse } from "@playwright/test";

interface DeletionImpact {
  planToken: string;
  requiredConfirmation: string;
  items: { key: string; action: string; count: number }[];
}

const PENDING_APPROVAL_EMAILS = "quote.pendingApprovalEmails";

/**
 * Approving a quote queues a decision email in the durable outbox. The hard
 * delete plan blocks while that email is still pending, so cleanup must wait
 * for the worker, the same way the approval helper waits for the request
 * email. Only a plan whose blockers are all pending approval emails qualifies.
 */
function waitsOnlyOnPendingApprovalEmails(impact: DeletionImpact) {
  const blocking = (impact.items ?? []).filter((item) => item.action === "Block" && item.count > 0);
  return blocking.length > 0 && blocking.every((item) => item.key === PENDING_APPROVAL_EMAILS);
}

interface ResourceDetail {
  rowVersion?: string;
}

interface HardDeleteOperation {
  operationId: string;
  isComplete: boolean;
  requiresManualAction: boolean;
  errorMessage?: string;
}

export async function hardDeleteBusinessRoot(
  api: APIRequestContext,
  headers: { Authorization: string },
  resourcePath: string,
) {
  let deleteResponse: APIResponse | undefined;
  const pendingEmailDeadline = Date.now() + 60_000;
  for (let attempt = 1; attempt <= 40; attempt += 1) {
    const detailResponse = await api.get(resourcePath, { headers });
    if (detailResponse.status() === 404) return;
    expect(detailResponse.status(), `read ${resourcePath} before cleanup`).toBe(200);

    const impactResponse = await api.get(`${resourcePath}/deletion-impact`, { headers });
    expect(impactResponse.status(), `preview ${resourcePath} cleanup`).toBe(200);

    const detail = (await detailResponse.json()) as ResourceDetail;
    const impact = (await impactResponse.json()) as DeletionImpact;
    deleteResponse = await api.delete(resourcePath, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data: {
        planToken: impact.planToken,
        confirmation: impact.requiredConfirmation,
        rowVersion: detail.rowVersion,
      },
    });
    if ([202, 204].includes(deleteResponse.status())) break;

    const errorBody = await deleteResponse.text();
    if (Date.now() < pendingEmailDeadline && waitsOnlyOnPendingApprovalEmails(impact)) {
      await new Promise((resolve) => setTimeout(resolve, 2_000));
      continue;
    }
    const isTransient = errorBody.includes("transient failure");
    if (!isTransient || attempt === 3) {
      expect([202, 204], errorBody).toContain(deleteResponse.status());
    }
    await new Promise((resolve) => setTimeout(resolve, attempt * 250));
  }

  expect(deleteResponse, `delete ${resourcePath}`).toBeDefined();
  if (!deleteResponse) return;
  if (deleteResponse.status() === 204) return;

  const acceptedOperation = (await deleteResponse.json()) as HardDeleteOperation;
  await expect
    .poll(
      async () => {
        const statusResponse = await api.get(
          `/api/hard-delete-operations/${acceptedOperation.operationId}`,
          { headers },
        );
        expect(statusResponse.status(), `read ${resourcePath} cleanup status`).toBe(200);
        const operation = (await statusResponse.json()) as HardDeleteOperation;
        expect(
          operation.requiresManualAction,
          operation.errorMessage ?? `${resourcePath} cleanup requires manual action`,
        ).toBe(false);
        return operation.isComplete;
      },
      {
        message: `wait for ${resourcePath} cleanup`,
        timeout: 30_000,
      },
    )
    .toBe(true);
}
