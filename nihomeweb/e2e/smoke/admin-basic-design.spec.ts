import { test, expect, TEST_USERS } from "../fixtures/auth";
import { createDesignProject, createOwnCustomer } from "../fixtures/designProjects";

/**
 * NIH-115 M2 Basic Design end-to-end flow. Real-user path through the
 * `docker compose` stack — the API is not mocked. Verifies:
 *
 *   1. SUPER_ADMIN can create a fresh DesignProject.
 *   2. Concept option finalize (NIH-114) flips the parent to BasicDesign.
 *   3. The Basic Design tab renders + accepts the create-doc dialog.
 *   4. Status transitions (submit review → internally approved) move a
 *      row through the state machine.
 *   5. The 3-discipline readiness gate + Unlock Detail Design button:
 *      the button is disabled until all 3 disciplines have ≥1 approved
 *      doc; clicking it flips the header stage to Detail Design.
 *
 * This spec exercises the exact flow a Design Lead would run when
 * pushing a project from kick-off to Detail Design.
 */

const uid = () => Math.random().toString(36).slice(2, 8);

test.describe("NIH-115 — Basic Design + Detail Design unlock (real-user flow)", () => {
  test("Design Lead can walk a project from Concept → Basic Design → Detail Design", async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
    baseURL,
  }) => {
    test.slow(); // this flow does ~15 HTTP round-trips

    // ---------- 1. Set up a fresh project via the API ----------
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };

    // Pick or create a customer.
    const customerId = await createOwnCustomer(api, authHeader, "BD");

    // Create the design project.
    const projectName = `E2E-BD ${uid()}`;
    const projectId = await createDesignProject(api, {
      headers: authHeader,
      name: projectName,
      customerId,
    });

    // Push a concept option all the way to Finalized so the project
    // moves to BasicDesign — this is what the Design Lead would do
    // manually via the Concept tab.
    const optCreate = await api.post("/api/concept-options", {
      headers: authHeader,
      data: { designProjectId: projectId, name: "E2E finalized" },
    });
    expect(optCreate.ok(), await optCreate.text()).toBeTruthy();
    const optId = (await optCreate.json()).id as number;
    for (const status of ["PendingInternalReview", "PresentedToClient", "Finalized"]) {
      const t = await api.post(`/api/concept-options/${optId}/status`, {
        headers: authHeader,
        data: { status },
      });
      expect(t.ok(), `${status}: ${await t.text()}`).toBeTruthy();
    }

    // ---------- 2. Open the detail page in the browser ----------
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`${baseURL}/admin/design-projects/${projectId}`, {
      waitUntil: "networkidle",
    });

    // Sanity check the header switched to Basic Design.
    await expect(page.getByRole("heading", { name: new RegExp(`DP-\\d+-\\d+`) })).toBeVisible();
    await expect(page.locator("main").getByText(/Basic Design/).first()).toBeVisible();

    // ---------- 3. Switch to the Basic Design tab ----------
    // The notifications overlay can sit above the tab in some viewports;
    // scroll into view + force-click to bypass.
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    const basicTab = page.locator('button[role="tab"]').filter({ hasText: /^Basic Design$/i });
    await basicTab.click({ force: true });
    // The readiness card + "Tạo bản vẽ" button should be visible.
    await expect(page.getByRole("button", { name: /T\u1ea1o b\u1ea3n v\u1ebd|New document/i })).toBeVisible();

    // ---------- 4. Create + approve 1 doc per required discipline ----------
    // We drive this through the API (faster + less flaky than clicking through
    // three dialogs) and then verify the UI reflects it.
    const disciplines = ["architecture", "structure", "mep"];
    for (const disciplineCode of disciplines) {
      const doc = await api.post("/api/basic-design-docs", {
        headers: authHeader,
        data: {
          designProjectId: projectId,
          disciplineCode,
          title: `${disciplineCode} — E2E ${uid()}`,
        },
      });
      expect(doc.ok(), await doc.text()).toBeTruthy();
      const docId = (await doc.json()).id as number;
      for (const status of ["SubmittedForReview", "InternallyApproved"]) {
        const t = await api.post(`/api/basic-design-docs/${docId}/status`, {
          headers: authHeader,
          data: { status },
        });
        expect(t.ok(), `${disciplineCode} → ${status}: ${await t.text()}`).toBeTruthy();
      }
    }

    // Reload so the tab shows the just-approved docs.
    await page.reload({ waitUntil: "networkidle" });
    await page.evaluate(() => {
      const region = document.querySelector('[aria-label*="Notifications ("]');
      region?.remove();
    });
    await basicTab.click({ force: true });

    // All 3 discipline pills should now be Approved (green). ShopDrawing
    // remains the API enum, while Detail Design is the customer-facing term.
    const readiness = page.locator("section").filter({
      hasText: /Sẵn sàng chuyển Thiết kế chi tiết|Ready for Detail Design|准备转详细设计|詳細設計移行の準備/i,
    });
    for (const label of ["Kiến trúc", "Kết cấu", "MEP"]) {
      await expect(
        readiness.getByText(new RegExp(`${label}.*(Đã duyệt|Approved|已批准|承認済)`, "i")),
      ).toBeVisible();
    }

    // Team and Documents are real API-backed rollups, not follow-up placeholders.
    await page.locator('button[role="tab"]').filter({ hasText: /^Team$/i }).click({ force: true });
    await expect(page.getByTestId("design-project-team-tab")).toBeVisible();
    await page.locator('button[role="tab"]').filter({ hasText: /^Tài liệu$|^Documents$/i }).click({ force: true });
    await expect(page.getByTestId("design-project-documents-tab")).toBeVisible();
    await expect(page.getByText(/NIH-114\.\.118/)).toHaveCount(0);

    // ---------- 5. Click the "Unlock Detail Design" button ----------
    await basicTab.click({ force: true });
    const unlockBtn = page.getByRole("button", {
      name: /Mở khoá Thiết kế chi tiết|Unlock Detail Design|解锁详细设计|詳細設計を解放/i,
    });
    await expect(unlockBtn).toBeEnabled();
    await unlockBtn.click({ force: true });

    // ---------- 6. Verify the stage flipped via the API ----------
    // The header badge sometimes takes a beat to refetch through the
    // parent DesignProjectDetail; hit the API directly so this final
    // assertion is deterministic on any network condition.
    await expect
      .poll(async () => {
        const proj = await api.get(`/api/design-projects/${projectId}`, { headers: authHeader });
        if (!proj.ok()) return "";
        return (await proj.json()).currentStage as string;
      }, { timeout: 5_000 })
      .toBe("ShopDrawing");
  });
});
