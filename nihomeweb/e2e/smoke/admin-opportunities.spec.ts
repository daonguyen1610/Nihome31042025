import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { APIRequestContext } from "@playwright/test";

/**
 * End-to-end smoke coverage for NIH-83 Opportunity module against the live
 * docker stack. Verifies the deployed HTTP surface (JWT auth, RBAC scoping,
 * stage transition rules, Kanban pipeline) plus the SPA route renders the
 * page without JS errors for a role that has access.
 */

function authed(api: APIRequestContext, token: string) {
    const auth = { Authorization: `Bearer ${token}` };
    return {
        get: (path: string) => api.get(path, { headers: auth }),
        post: (path: string, data: unknown) => api.post(path, { headers: auth, data }),
        put: (path: string, data: unknown) => api.put(path, { headers: auth, data }),
        patch: (path: string, data: unknown) => api.patch(path, { headers: auth, data }),
        del: (path: string, data?: unknown) => api.delete(path, { headers: auth, data }),
    };
}

async function deleteOpportunity(api: APIRequestContext, token: string, opportunityId: number) {
    const client = authed(api, token);
    const [impactResponse, detailResponse] = await Promise.all([
        client.get(`/api/opportunities/${opportunityId}/deletion-impact`),
        client.get(`/api/opportunities/${opportunityId}`),
    ]);
    expect(impactResponse.status(), `preview opportunity ${opportunityId}`).toBe(200);
    expect(detailResponse.status(), `read opportunity ${opportunityId}`).toBe(200);
    const impact = await impactResponse.json() as { planToken: string; requiredConfirmation: string };
    const detail = await detailResponse.json() as { rowVersion: string };
    return client.del(`/api/opportunities/${opportunityId}`, {
        planToken: impact.planToken,
        confirmation: impact.requiredConfirmation,
        rowVersion: detail.rowVersion,
    });
}

async function createCustomer(api: APIRequestContext, token: string): Promise<number> {
    const c = authed(api, token);
    const suffix = Math.random().toString(36).slice(2, 8);
    const res = await c.post("/api/customers", {
        type: "Individual",
        name: `[E2E-OPP] Customer ${suffix}`,
        sourceCode: "marketing",
        primaryContact: {
            fullName: `E2E Contact ${suffix}`,
            phone: `0987${Math.floor(1_000_000 + Math.random() * 8_000_000)}`,
            email: `opp-${suffix}@test.example`,
            isPrimary: true,
        },
    });
    expect(res.status(), "create customer for opp e2e").toBe(201);
    const body = await res.json();
    return body.id as number;
}

async function createOpportunity(
    api: APIRequestContext,
    token: string,
    customerId: number,
    overrides: Record<string, unknown> = {},
): Promise<number> {
    const c = authed(api, token);
    const res = await c.post("/api/opportunities", {
        name: `[E2E-OPP] Deal ${Math.random().toString(36).slice(2, 8)}`,
        customerId,
        estimatedValue: 1_000_000,
        winProbability: 25,
        ...overrides,
    });
    expect(res.status(), "create opportunity e2e").toBe(201);
    const body = await res.json();
    return body.id as number;
}

test("SALE can CRUD only their own opportunities", async ({ api, loginAs }) => {
    const saleToken = await loginAs(TEST_USERS.sale);
    const managerToken = await loginAs(TEST_USERS.salesManager);

    // Manager creates an opportunity that SALE does not own.
    const foreignCustomer = await createCustomer(api, managerToken);
    const foreignOp = await createOpportunity(api, managerToken, foreignCustomer);

    // SALE list must not include the manager-owned row.
    const saleList = await authed(api, saleToken).get("/api/opportunities?pageSize=100");
    expect(saleList.status()).toBe(200);
    const list = await saleList.json();
    expect((list.items as Array<{ id: number }>).some((o) => o.id === foreignOp)).toBeFalsy();

    // Direct GET should 404 to hide the resource entirely.
    const direct = await authed(api, saleToken).get(`/api/opportunities/${foreignOp}`);
    expect(direct.status()).toBe(404);

    // DELETE 404 as well — regression guard for the CRM cross-owner leak fixed in NIH-78.
    const del = await authed(api, saleToken).del(`/api/opportunities/${foreignOp}`, {
        planToken: "a".repeat(64),
        confirmation: `OPPORTUNITY-${foreignOp}`,
        rowVersion: "AAAAAAAAAAA=",
    });
    expect(del.status()).toBe(404);

    // Manager still sees the row afterwards.
    const stillThere = await authed(api, managerToken).get(`/api/opportunities/${foreignOp}`);
    expect(stillThere.status()).toBe(200);
    expect((await deleteOpportunity(api, managerToken, foreignOp)).status()).toBe(204);
});

test("Stage transition deployment smoke — next stage succeeds and Lost requires reason + note", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const customerId = await createCustomer(api, token);
    const c = authed(api, token);

    // Path 1 — forward transition Prospecting → Qualification succeeds and appends a StageChange activity.
    const opA = await createOpportunity(api, token, customerId);
    const fwd = await c.patch(`/api/opportunities/${opA}/stage`, { targetStage: "Qualification" });
    expect(fwd.status()).toBe(200);
    const detailA = await c.get(`/api/opportunities/${opA}`);
    const bodyA = await detailA.json();
    expect(bodyA.stage).toBe("Qualification");
    expect((bodyA.activities as Array<{ type: string }>).some((a) => a.type === "StageChange")).toBeTruthy();

    // Path 2 — Lost requires reason + note.
    const opB = await createOpportunity(api, token, customerId);
    const missingReason = await c.patch(`/api/opportunities/${opB}/stage`, { targetStage: "Lost" });
    expect(missingReason.status()).toBe(400);
    const invalidReason = await c.patch(`/api/opportunities/${opB}/stage`, {
        targetStage: "Lost",
        lostReasonCode: "invented-reason",
        lostNote: "n/a",
    });
    expect(invalidReason.status()).toBe(400);
    const okLost = await c.patch(`/api/opportunities/${opB}/stage`, {
        targetStage: "Lost",
        lostReasonCode: "price",
        lostNote: "E2E: khách hỏi giá cao.",
    });
    expect(okLost.status()).toBe(200);

    // Path 3 — skipping directly to Won is blocked because the sequential stages
    // and a linked signed contract are required.
    const opC = await createOpportunity(api, token, customerId);
    const skipped = await c.patch(`/api/opportunities/${opC}/stage`, { targetStage: "Won" });
    expect(skipped.status()).toBe(400);

    // Clean up.
    expect((await deleteOpportunity(api, token, opA)).status()).toBe(204);
    expect((await deleteOpportunity(api, token, opB)).status()).toBe(204);
    expect((await deleteOpportunity(api, token, opC)).status()).toBe(204);
});

test("Pipeline endpoint returns six columns with totals aggregated server-side", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const c = authed(api, token);

    const res = await c.get("/api/opportunities/pipeline");
    expect(res.status()).toBe(200);
    const body = await res.json();
    const columns = body.columns as Array<{ stage: string; count: number; totalValue: number }>;
    expect(columns).toHaveLength(6);
    expect(columns.map((c) => c.stage)).toEqual([
        "Prospecting",
        "Qualification",
        "Proposal",
        "Negotiation",
        "Won",
        "Lost",
    ]);
    // Each column has a numeric count + totalValue (server-side aggregation, not client-side).
    for (const col of columns) {
        expect(typeof col.count).toBe("number");
        expect(typeof col.totalValue).toBe("number");
    }
});

test("Roles without crm.opportunities.view get 403 on the endpoint", async ({ api, loginAs }) => {
    for (const role of [TEST_USERS.warehouse, TEST_USERS.design, TEST_USERS.qs] as const) {
        const token = await loginAs(role);
        const res = await authed(api, token).get("/api/opportunities");
        expect(res.status(), `${role.role} must not access opportunities`).toBe(403);
    }
});

test("SPA renders /admin/opportunities without console errors for SALES_MANAGER", async ({ page, loginInBrowserAs, baseURL }) => {
    const jsErrors: string[] = [];
    page.on("pageerror", (err) => jsErrors.push(err.message));

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`${baseURL}/admin/opportunities`, { waitUntil: "networkidle" });

    await expect(page.getByRole("heading", { name: /Cơ hội|Opportunit/i })).toBeVisible();
    // Table view button exists (default view). Pipeline toggle available.
    await expect(page.getByRole("button", { name: /^Bảng$|Table/ })).toBeVisible();
    await expect(page.getByRole("button", { name: /^Pipeline$/ })).toBeVisible();

    expect(jsErrors, `Unexpected JS errors: ${jsErrors.join("\n")}`).toHaveLength(0);
});

test("Opportunity actions follow the sequential and terminal UI contract", async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
    baseURL,
}) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const customerId = await createCustomer(api, token);
    const opportunityId = await createOpportunity(api, token, customerId);
    const client = authed(api, token);
    const contractSignedAction = page.getByRole("button", {
        name: /Xác nhận ký hợp đồng|Confirm contract signed/i,
    });

    await loginInBrowserAs(page, TEST_USERS.salesManager);
    await page.goto(`${baseURL}/admin/opportunities/${opportunityId}`, { waitUntil: "networkidle" });
    await expect(contractSignedAction).toHaveCount(0);

    for (const targetStage of ["Qualification", "Proposal", "Negotiation"]) {
        const response = await client.patch(`/api/opportunities/${opportunityId}/stage`, { targetStage });
        expect(response.status(), `move opportunity to ${targetStage}`).toBe(200);
    }
    await page.reload({ waitUntil: "networkidle" });
    await expect(contractSignedAction).toBeVisible();

    const lost = await client.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Lost",
        lostReasonCode: "price",
        lostNote: "E2E: kết thúc cơ hội để xác minh chế độ chỉ đọc.",
    });
    expect(lost.status()).toBe(200);
    const terminalDetail = await client.get(`/api/opportunities/${opportunityId}`);
    const opportunityName = ((await terminalDetail.json()) as { name: string }).name;

    await page.goto(`${baseURL}/admin/opportunities`, { waitUntil: "networkidle" });
    await page.getByLabel(/Tìm kiếm|Search/i).fill(opportunityName);
    const desktopRow = page.getByRole("row").filter({ hasText: opportunityName });
    await expect(desktopRow).toBeVisible();
    await expect(desktopRow.getByRole("button", { name: /^Sửa$|^Edit$/i })).toHaveCount(0);

    await page.setViewportSize({ width: 390, height: 844 });
    const mobileCard = page.getByTestId(`opportunity-card-${opportunityId}`);
    await expect(mobileCard).toBeVisible();
    await expect(mobileCard).toContainText(opportunityName);
    await expect(mobileCard.getByRole("button", { name: /^Sửa$|^Edit$/i })).toHaveCount(0);

    await client.del(`/api/opportunities/${opportunityId}`);
});
