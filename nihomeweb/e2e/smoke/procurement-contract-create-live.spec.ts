import { expect, test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";
import { hardDeleteBusinessRoot } from "../fixtures/hardDelete";

test("creating from the project supplier registry keeps Downstream direction", async ({
  api,
  page,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const customerId = await createOwnCustomer(
    api,
    headers,
    "NICON – Hợp đồng cung cấp thép Long Thành",
  );
  let projectId: number | undefined;
  try {
    const response = await api.post("/api/operational-projects", {
      headers,
      data: { customerId, name: "NICON – Lập hợp đồng cung ứng từ dự án" },
    });
    expect(response.status(), await response.text()).toBe(201);
    projectId = (await response.json()).id;
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/");
    await page.evaluate(() => localStorage.setItem("nicon_lang", "en"));
    await page.goto(`/admin/contracts?direction=Downstream&operationalProjectId=${projectId}`);
    await page.getByRole("button", { name: "New contract", exact: true }).click();
    // The direction select shows the localized direction name; the seeded
    // English label and the legacy one both identify the Downstream option.
    await expect(page.locator("#c-direction-form")).toContainText(
      /Downstream|Purchase – supplier\/subcontractor/,
    );
    await expect(page.getByRole("dialog")).toContainText("NICON – Lập hợp đồng cung ứng từ dự án");
  } finally {
    if (projectId)
      await hardDeleteBusinessRoot(api, headers, `/api/operational-projects/${projectId}`);
    await hardDeleteBusinessRoot(api, headers, `/api/customers/${customerId}`);
  }
});
