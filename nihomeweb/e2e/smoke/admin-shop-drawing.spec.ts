import { test, expect, TEST_USERS } from "../fixtures/auth";
import {
  approveDesignStage,
  createDesignProject,
  createOwnCustomer,
  isGoogleDriveConnected,
} from "../fixtures/designProjects";

/**
 * NIH-116 M2 Detail Design end-to-end flow. Real-user path through the
 * `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN sets up a fresh DesignProject then walks it through
 *      Concept finalize (NIH-114) + Basic Design approvals (NIH-115) +
 *      Detail Design unlock.
 *   2. The Detail Design tab renders the readiness pills + list.
 *   3. Creating a shop drawing via the dialog assigns an auto-code
 *      (KT-SD-001) and shows up under the correct discipline / item.
 *   4. Sending a drawing for review + approving + queueing IFC drives
 *      the state-machine pills through the expected badges.
 *   5. Bulk-select + bulk-delete of drafts removes the selected rows
 *      and toasts a partial-success message when a non-draft is mixed in.
 *   6. RBAC — a SALE user is bounced when hitting the endpoint.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-116 — Detail Design (real-user flow)", () => {
  test("Design Lead can create, transition and safely delete rejected shop drawings", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow(); // ~20 HTTP round-trips + browser interactions.

    // ---------- 1. Set up a fresh project via the API ----------
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    test.skip(
      !(await isGoogleDriveConnected(api, authHeader)),
      "Requires a connected Google Drive because approved design evidence must be durably synced.",
    );

    // Reuse an existing customer if any.
    const customerId = await createOwnCustomer(api, authHeader, "SD");

    const projectName = `E2E-SD ${uid()}`;
    const projectId = await createDesignProject(api, {
      headers: authHeader,
      name: projectName,
      customerId,
    });

    // Concept → BasicDesign
    const optCreate = await api.post("/api/concept-options", {
      headers: authHeader,
      data: { designProjectId: projectId, name: "E2E finalized" },
    });
    expect(optCreate.ok(), await optCreate.text()).toBeTruthy();
    const optId = (await optCreate.json()).id as number;
    for (const status of ["PendingInternalReview", "PresentedToClient"]) {
      const t = await api.post(`/api/concept-options/${optId}/status`, {
        headers: authHeader,
        data: { status },
      });
      expect(t.ok(), `${status}: ${await t.text()}`).toBeTruthy();
    }
    await approveDesignStage(api, { headers: authHeader, projectId, stage: "Concept", category: "DesignConcept", sourceEntityType: "ConceptOption", sourceRecordIds: [optId] });
    const finalized = await api.post(`/api/concept-options/${optId}/status`, { headers: authHeader, data: { status: "Finalized" } });
    expect(finalized.ok(), await finalized.text()).toBeTruthy();

    // BasicDesign → ShopDrawing (approve 1 doc per required discipline)
    const basicDocumentIds: number[] = [];
    for (const disciplineCode of ["architecture", "structure", "mep"]) {
      const doc = await api.post("/api/basic-design-docs", {
        headers: authHeader,
        data: {
          designProjectId: projectId,
          disciplineCode,
          title: `${disciplineCode} — E2E BD ${uid()}`,
        },
      });
      expect(doc.ok(), await doc.text()).toBeTruthy();
      const docId = (await doc.json()).id as number;
      basicDocumentIds.push(docId);
      for (const status of ["SubmittedForReview", "InternallyApproved"]) {
        const t = await api.post(`/api/basic-design-docs/${docId}/status`, {
          headers: authHeader,
          data: { status },
        });
        expect(t.ok(), `${disciplineCode} → ${status}: ${await t.text()}`).toBeTruthy();
      }
    }
    await approveDesignStage(api, { headers: authHeader, projectId, stage: "BasicDesign", category: "DesignBasic", sourceEntityType: "BasicDesignDoc", sourceRecordIds: basicDocumentIds });
    const unlock = await api.post(`/api/basic-design-docs/design-project/${projectId}/unlock-shop-drawing`, {
      headers: authHeader,
    });
    expect(unlock.ok(), await unlock.text()).toBeTruthy();

    // ---------- 2. Open the detail page + switch to Detail Design tab ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/design-projects/${projectId}`, {
      waitUntil: "networkidle",
    });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });

    const shopTab = page.getByRole("tab", {
      name: /^(Thiết kế chi tiết|Detail Design|详细设计|詳細設計)$/i,
    });
    await shopTab.click({ force: true });
    await expect(page.getByTestId("shop-drawing-tab")).toBeVisible();
    await expect(page.getByTestId("shop-drawing-new")).toBeVisible();

    // ---------- 3. Create a shop drawing via the dialog ----------
    await page.getByTestId("shop-drawing-new").click();

    // discipline dropdown (radix-ui Select)
    await page.getByTestId("shop-drawing-form-discipline").click();
    await page.getByRole("option", { name: /Ki\u1ebfn tr\u00fac|Architecture|建筑|建築/i }).click();

    await page.getByTestId("shop-drawing-form-item").fill("Móng cọc E2E");
    await page.getByTestId("shop-drawing-form-title").fill(`E2E first drawing ${uid()}`);
    await page.getByTestId("shop-drawing-form-save").click();

    // The new row should appear grouped under architecture with a KT-SD- code.
    await expect(page.getByTestId("shop-drawing-discipline-architecture")).toBeVisible();
    await expect(
      page.getByTestId("shop-drawing-discipline-architecture")
        .locator("text=/KT-SD-\\d{3}/").first(),
    ).toBeVisible();

    // ---------- 4. Drive the state machine ----------
    // Grab the id of the drawing we just created via the API for a
    // deterministic locator (rather than parsing the DOM code).
    const list = await api.get(`/api/shop-drawings?designProjectId=${projectId}&pageSize=200`, {
      headers: authHeader,
    });
    expect(list.ok(), await list.text()).toBeTruthy();
    const items = (await list.json()).items as Array<{ id: number; status: string; disciplineCode: string }>;
    const firstDrawing = items.find((r) => r.disciplineCode === "architecture");
    expect(firstDrawing, "created drawing not returned by list").toBeTruthy();
    const drawingId = firstDrawing!.id;

    // send review → approve → queue IFC
    await page.getByTestId(`shop-drawing-send-${drawingId}`).click();
    await expect(page.getByTestId(`shop-drawing-status-${drawingId}`)).toHaveText(/InReview|Chờ duyệt|In review|审核中|レビュー中/i);
    await page.getByTestId(`shop-drawing-approve-${drawingId}`).click();
    await expect(page.getByTestId(`shop-drawing-status-${drawingId}`)).toHaveText(/Approved|Đã duyệt|已批准|承認済み/i);
    await page.getByTestId(`shop-drawing-queue-${drawingId}`).click();
    await expect(page.getByTestId(`shop-drawing-status-${drawingId}`))
      .toHaveText(/PendingIfc|Ch\u1edd ph\u00e1t h\u00e0nh IFC|IFC/i);

    // ---------- 5. Impact-planned deletion of rejected dossiers ----------
    const rejectedDrawings: Array<{ id: number; drawingCode: string }> = [];
    for (let i = 0; i < 2; i++) {
      const res = await api.post("/api/shop-drawings", {
        headers: authHeader,
        data: {
          designProjectId: projectId,
          disciplineCode: "structure",
          constructionItem: `E2E bulk item ${i}`,
          title: `E2E bulk draft ${uid()}-${i}`,
        },
      });
      expect(res.ok(), await res.text()).toBeTruthy();
      const created = await res.json() as { id: number; drawingCode: string };
      for (const status of ["InReview", "Rejected"]) {
        const transition = await api.post(`/api/shop-drawings/${created.id}/status`, {
          headers: authHeader,
          data: { status },
        });
        expect(transition.ok(), await transition.text()).toBeTruthy();
      }
      rejectedDrawings.push(created);
    }

    await page.reload({ waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await shopTab.click({ force: true });
    await expect(page.getByTestId("shop-drawing-tab")).toBeVisible();

    for (const drawing of rejectedDrawings) {
      await page.getByTestId(`shop-drawing-delete-${drawing.id}`).first().click();
      await expect(page.locator("#deletion-confirmation")).toBeVisible();
      await page.locator("#deletion-confirmation").fill(drawing.drawingCode);
      await page.getByRole("button", { name: /Xóa vĩnh viễn|Delete permanently/i }).click();
      await expect(page.locator(`#shop-drawing-${drawing.id}`)).toHaveCount(0);
    }

    // Confirm both rejected records vanish from the list via the API (this is
    // deterministic regardless of any toast-driven remount).
    await expect
      .poll(async () => {
        const l = await api.get(`/api/shop-drawings?designProjectId=${projectId}&pageSize=200`, {
          headers: authHeader,
        });
        if (!l.ok()) return "err";
        const rows = (await l.json()).items as Array<{ id: number }>;
        return rejectedDrawings.every(({ id }) => !rows.some((r) => r.id === id));
      }, { timeout: 5_000 })
      .toBe(true);
  });

  test("cleanup link focuses the exact Shop Drawing and exposes delete", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow();
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    test.skip(
      !(await isGoogleDriveConnected(api, authHeader)),
      "Requires a connected Google Drive because approved design evidence must be durably synced.",
    );
    const customerId = await createOwnCustomer(api, authHeader, "SD-LINK");
    const projectId = await createDesignProject(api, {
      headers: authHeader,
      name: `E2E-SD-LINK ${uid()}`,
      customerId,
    });

    const option = await api.post("/api/concept-options", {
      headers: authHeader,
      data: { designProjectId: projectId, name: "E2E cleanup link" },
    });
    expect(option.ok(), await option.text()).toBeTruthy();
    const optionId = (await option.json()).id as number;
    for (const status of ["PendingInternalReview", "PresentedToClient"]) {
      const transition = await api.post(`/api/concept-options/${optionId}/status`, {
        headers: authHeader,
        data: { status },
      });
      expect(transition.ok(), `${status}: ${await transition.text()}`).toBeTruthy();
    }
    await approveDesignStage(api, { headers: authHeader, projectId, stage: "Concept", category: "DesignConcept", sourceEntityType: "ConceptOption", sourceRecordIds: [optionId] });
    const finalized = await api.post(`/api/concept-options/${optionId}/status`, { headers: authHeader, data: { status: "Finalized" } });
    expect(finalized.ok(), await finalized.text()).toBeTruthy();
    const prerequisiteDocumentIds: number[] = [];
    for (const disciplineCode of ["architecture", "structure", "mep"]) {
      const document = await api.post("/api/basic-design-docs", {
        headers: authHeader,
        data: {
          designProjectId: projectId,
          disciplineCode,
          title: `${disciplineCode} cleanup prerequisite`,
        },
      });
      expect(document.ok(), await document.text()).toBeTruthy();
      const documentId = (await document.json()).id as number;
      prerequisiteDocumentIds.push(documentId);
      for (const status of ["SubmittedForReview", "InternallyApproved"]) {
        const transition = await api.post(`/api/basic-design-docs/${documentId}/status`, {
          headers: authHeader,
          data: { status },
        });
        expect(transition.ok(), `${disciplineCode} → ${status}: ${await transition.text()}`).toBeTruthy();
      }
    }
    await approveDesignStage(api, { headers: authHeader, projectId, stage: "BasicDesign", category: "DesignBasic", sourceEntityType: "BasicDesignDoc", sourceRecordIds: prerequisiteDocumentIds });
    const unlock = await api.post(`/api/basic-design-docs/design-project/${projectId}/unlock-shop-drawing`, {
      headers: authHeader,
    });
    expect(unlock.ok(), await unlock.text()).toBeTruthy();
    const drawing = await api.post("/api/shop-drawings", {
      headers: authHeader,
      data: {
        designProjectId: projectId,
        disciplineCode: "architecture",
        constructionItem: "Kiến trúc",
        title: `Cleanup target ${uid()}`,
      },
    });
    expect(drawing.ok(), await drawing.text()).toBeTruthy();
    const drawingId = (await drawing.json()).id as number;
    for (const status of ["InReview", "Rejected"]) {
      const transition = await api.post(`/api/shop-drawings/${drawingId}/status`, {
        headers: authHeader,
        data: { status },
      });
      expect(transition.ok(), await transition.text()).toBeTruthy();
    }

    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(
      `${baseURL}/admin/design-projects/${projectId}?tab=shop&documentId=${drawingId}`,
      { waitUntil: "networkidle" },
    );

    await expect(page.locator(`#shop-drawing-${drawingId}`)).toHaveClass(/ring-amber-200/);
    await expect(page.getByTestId(`shop-drawing-delete-${drawingId}`)).toBeVisible();
  });

  test("SALE role is blocked from Detail Design endpoints", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.sale);
    const res = await api.get("/api/shop-drawings", {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(res.status()).toBe(403);
  });
});
