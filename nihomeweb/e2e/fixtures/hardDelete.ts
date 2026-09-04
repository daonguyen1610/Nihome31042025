import { expect, type APIRequestContext } from "@playwright/test";

interface DeletionImpact {
  planToken: string;
  requiredConfirmation: string;
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
  const detailResponse = await api.get(resourcePath, { headers });
  if (detailResponse.status() === 404) return;
  expect(detailResponse.status(), `read ${resourcePath} before cleanup`).toBe(200);

  const impactResponse = await api.get(`${resourcePath}/deletion-impact`, { headers });
  expect(impactResponse.status(), `preview ${resourcePath} cleanup`).toBe(200);

  const detail = await detailResponse.json() as ResourceDetail;
  const impact = await impactResponse.json() as DeletionImpact;
  const deleteResponse = await api.delete(resourcePath, {
    headers,
    data: {
      planToken: impact.planToken,
      confirmation: impact.requiredConfirmation,
      rowVersion: detail.rowVersion,
    },
  });
  expect([202, 204], await deleteResponse.text()).toContain(deleteResponse.status());
  if (deleteResponse.status() === 204) return;

  const acceptedOperation = await deleteResponse.json() as HardDeleteOperation;
  await expect.poll(async () => {
    const statusResponse = await api.get(
      `/api/hard-delete-operations/${acceptedOperation.operationId}`,
      { headers },
    );
    expect(statusResponse.status(), `read ${resourcePath} cleanup status`).toBe(200);
    const operation = await statusResponse.json() as HardDeleteOperation;
    expect(
      operation.requiresManualAction,
      operation.errorMessage ?? `${resourcePath} cleanup requires manual action`,
    ).toBe(false);
    return operation.isComplete;
  }, {
    message: `wait for ${resourcePath} cleanup`,
    timeout: 30_000,
  }).toBe(true);
}