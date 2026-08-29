import type { Page } from "@playwright/test";

import { expect, test, TEST_USERS } from "../fixtures/auth";

const entityTypes = [
  { type: "Activity", label: "Activities", fields: ["Title", "Excerpt", "Content"] },
  { type: "News", label: "News", fields: ["Title", "Excerpt", "Content"] },
  { type: "Project", label: "Projects", fields: ["Name", "Description", "Content", "Challenges", "Solutions", "Highlights"] },
  { type: "Service", label: "Services", fields: ["Title", "Short title", "Tagline", "Introduction", "Highlights", "Content sections", "Introduction blocks"] },
  { type: "Slideshow", label: "Homepage slideshow", fields: ["Title", "Subtitle", "Link label"] },
  { type: "JobPosition", label: "Job positions", fields: ["Title", "Department", "Description", "Requirements"] },
  { type: "About", label: "About sections", fields: ["Eyebrow label", "Title part one", "Title part two", "Description paragraph one", "Description paragraph two", "Structured content items"] },
  { type: "ActivityCategory", label: "Activity categories", fields: ["Name"] },
  { type: "NewsCategory", label: "News categories", fields: ["Name"] },
  { type: "ProjectCategory", label: "Project categories", fields: ["Name"] },
  { type: "AsBuiltDocumentCategory", label: "As-built document categories", fields: ["Name"] },
] as const;

const authorizationHeaders = (token: string) => ({ Authorization: `Bearer ${token}` });

async function useEnglish(page: Page) {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
}

async function loadEntityRegistry(page: Page) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().endsWith("/api/translations/entity/types")
      && response.request().method() === "GET",
  );
  await page.goto("/admin/translations?tab=entity&type=Activity");
  const response = await responsePromise;
  expect(response.ok(), "load Content Translations registry").toBeTruthy();
  return response.json() as Promise<Array<{ type: string }>>;
}

async function openEntityType(page: Page, entityType: string) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes(`/api/translations/entity/${entityType}`)
      && response.request().method() === "GET"
      && !response.url().includes(`/entity/${entityType}/`),
  );
  await page.goto(`/admin/translations?tab=entity&type=${entityType}`);
  const response = await responsePromise;
  expect(response.ok(), `load ${entityType} translation list`).toBeTruthy();
  const body = await response.json() as { items: Array<{ id: number; title: string }> };
  return body.items;
}

async function openEntityEditor(page: Page, entityType: string, entityId: number) {
  const card = page.locator("div.rounded-lg.border.bg-card", {
    has: page.getByText(`#${entityId}`, { exact: true }),
  });
  await expect(card).toBeVisible();
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes(`/api/translations/entity/${entityType}/${entityId}`)
      && response.request().method() === "GET",
  );
  await card.getByRole("button").click();
  const response = await responsePromise;
  expect(response.ok(), `load ${entityType} #${entityId} translation detail`).toBeTruthy();
  const editor = page.getByRole("dialog");
  await expect(editor).toBeVisible();
  return editor;
}

function translatedField(editor: ReturnType<Page["getByRole"]>, label: string) {
  return editor.getByText(`🇺🇸 English ${label}`, { exact: true })
    .locator("..")
    .locator("textarea");
}

test.describe("Content translations — browser coverage", () => {
  test("renders all supported domains and their registered fields", async ({
    page,
    loginInBrowserAs,
  }) => {
    test.slow();
    await useEnglish(page);
    await loginInBrowserAs(page, TEST_USERS.admin);
    const registry = await loadEntityRegistry(page);
    expect(registry.map((entry) => entry.type).sort()).toEqual(
      entityTypes.map((entry) => entry.type).sort(),
    );

    for (const entityType of entityTypes) {
      const items = await openEntityType(page, entityType.type);
      expect(items.length, `${entityType.label} needs seeded showcase data`).toBeGreaterThan(0);

      const editor = await openEntityEditor(page, entityType.type, items[0].id);
      await expect(editor.getByText(`${entityType.label} #${items[0].id}`, { exact: true })).toBeVisible();
      await expect(editor.locator("textarea")).toHaveCount(entityType.fields.length * 2);

      for (const field of entityType.fields) {
        await expect(editor.getByText(`🇻🇳 ${field} (Source)`, { exact: true })).toBeVisible();
        await expect(editor.getByText(`🇺🇸 English ${field}`, { exact: true })).toBeVisible();
      }

      await page.keyboard.press("Escape");
      await expect(editor).toBeHidden();
    }
  });

  test("admin saves, reloads, and resets a slideshow translation through the UI", async ({
    api,
    page,
    loginAs,
    loginInBrowserAs,
  }) => {
    const token = await loginAs(TEST_USERS.admin);
    const suffix = crypto.randomUUID().slice(0, 8);
    const title = `E2E translation ${suffix}`;
    const translatedTitle = `Translated title ${suffix}`;
    const createResponse = await api.post("/api/slideshow", {
      headers: authorizationHeaders(token),
      data: {
        slug: `e2e-translation-${suffix}`,
        imageUrl: "/images/activities/nicon_hoa_binh.jpg",
        title,
        subtitle: "Translation lifecycle source",
        linkUrl: "/about",
        linkText: "View source",
        isActive: true,
        sortOrder: 999,
      },
    });
    expect(createResponse.status()).toBe(201);
    const created = await createResponse.json() as { id: number };

    try {
      await useEnglish(page);
      await loginInBrowserAs(page, TEST_USERS.admin);
      await openEntityType(page, "Slideshow");

      let editor = await openEntityEditor(page, "Slideshow", created.id);
      await translatedField(editor, "Title").fill(translatedTitle);
      await editor.getByRole("button", { name: "Save changes", exact: true }).click();
      await expect(editor).toBeHidden();

      const card = page.locator("div.rounded-lg.border.bg-card", {
        has: page.getByText(`#${created.id}`, { exact: true }),
      });
      await expect(card).toContainText("1/9");

      editor = await openEntityEditor(page, "Slideshow", created.id);
      await expect(translatedField(editor, "Title")).toHaveValue(translatedTitle);

      page.once("dialog", (dialog) => dialog.accept());
      await editor.getByRole("button", { name: "Reset translations", exact: true }).click();
      await expect(editor).toBeHidden();
      await expect(card).toContainText("Not translated");

      editor = await openEntityEditor(page, "Slideshow", created.id);
      await expect(translatedField(editor, "Title")).toHaveValue("");
    } finally {
      await api.delete(`/api/slideshow/${created.id}`, {
        headers: authorizationHeaders(token),
      });
    }
  });

  test("rejects malformed structured JSON before sending the save request", async ({
    page,
    loginInBrowserAs,
  }) => {
    await useEnglish(page);
    await loginInBrowserAs(page, TEST_USERS.admin);
    const items = await openEntityType(page, "About");
    expect(items.length, "About needs seeded showcase data").toBeGreaterThan(0);

    const editor = await openEntityEditor(page, "About", items[0].id);
    await translatedField(editor, "Structured content items").fill("{not-valid-json");

    let saveRequests = 0;
    page.on("request", (request) => {
      if (request.method() === "POST" && request.url().includes(`/api/translations/entity/About/${items[0].id}`)) {
        saveRequests += 1;
      }
    });
    await editor.getByRole("button", { name: "Save changes", exact: true }).click();

    await expect(page.getByText(/Invalid JSON/).last()).toBeVisible();
    await expect(editor).toBeVisible();
    expect(saveRequests).toBe(0);
  });

  test("view-only role can inspect content translations without write actions", async ({
    page,
    loginInBrowserAs,
  }) => {
    await useEnglish(page);
    await loginInBrowserAs(page, TEST_USERS.bgd);
    const items = await openEntityType(page, "Activity");
    expect(items.length, "Activities need seeded showcase data").toBeGreaterThan(0);

    const editor = await openEntityEditor(page, "Activity", items[0].id);
    const textareas = editor.locator("textarea");
    await expect(textareas).not.toHaveCount(0);
    for (let index = 0; index < await textareas.count(); index += 1) {
      await expect(textareas.nth(index)).toHaveAttribute("readonly", "");
    }
    await expect(editor.getByRole("button", { name: "Save changes", exact: true })).toHaveCount(0);
    await expect(editor.getByRole("button", { name: "Reset translations", exact: true })).toHaveCount(0);
  });

  test("design lead can access content translation write actions", async ({
    page,
    loginInBrowserAs,
  }) => {
    await useEnglish(page);
    await loginInBrowserAs(page, TEST_USERS.designLead);
    const items = await openEntityType(page, "Activity");
    expect(items.length, "Activities need seeded showcase data").toBeGreaterThan(0);

    const editor = await openEntityEditor(page, "Activity", items[0].id);
    await expect(editor.getByRole("button", { name: "Save changes", exact: true })).toBeVisible();
    await expect(editor.getByRole("button", { name: "Reset translations", exact: true })).toBeVisible();
  });
});
