import { expect, type APIRequestContext } from "@playwright/test";

type Quote = { id: number; rowVersion: string; status: string };

/** Submit with the exact rendered email that the selected approver will receive. */
export async function submitAndApproveQuote(
  api: APIRequestContext,
  senderHeaders: Record<string, string>,
  approverHeaders: Record<string, string>,
  quoteId: number,
  rowVersion: string,
): Promise<Quote> {
  const candidates = await api.get(`/api/quotes/${quoteId}/approval-candidates`, {
    headers: senderHeaders,
  });
  expect(candidates.status(), await candidates.text()).toBe(200);
  const approver = ((await candidates.json()) as { id: number; name: string }[]).find(
    (candidate) => candidate.name === "Super Admin",
  );
  expect(approver, "Super Admin must be an eligible, independent quote approver").toBeTruthy();

  const preview = await api.post(`/api/quotes/${quoteId}/approval-email-preview`, {
    headers: senderHeaders,
    data: { approverUserId: approver!.id, rowVersion },
  });
  expect(preview.status(), await preview.text()).toBe(200);
  const { previewToken } = (await preview.json()) as { previewToken: string };
  const submitted = await api.post(`/api/quotes/${quoteId}/submit`, {
    headers: senderHeaders,
    data: { approverUserId: approver!.id, previewToken, rowVersion },
  });
  expect(submitted.status(), await submitted.text()).toBe(200);
  const pending = (await submitted.json()) as Quote;
  expect(pending.status).toBe("PendingApproval");
  await expect
    .poll(
      async () => {
        const response = await api.get(`/api/quotes/${quoteId}`, { headers: senderHeaders });
        expect(response.status(), await response.text()).toBe(200);
        return ((await response.json()) as { approvalEmailStatus: string }).approvalEmailStatus;
      },
      { timeout: 25_000 },
    )
    .toBe("Sent");

  const approved = await api.post(`/api/quotes/${quoteId}/approve`, {
    headers: approverHeaders,
    data: { rowVersion: pending.rowVersion },
  });
  expect(approved.status(), await approved.text()).toBe(200);
  const result = (await approved.json()) as Quote;
  expect(result.status).toBe("Approved");
  return result;
}
