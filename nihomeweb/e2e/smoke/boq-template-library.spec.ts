import { readFile } from "node:fs/promises";
import path from "node:path";
import { expect, test, TEST_USERS } from "../fixtures/auth";

const templatePath = path.resolve(
  process.cwd(),
  "../nihomebackend/Data/BoqTemplates/BOQ-PCCC-Can-ho-dich-vu.xlsx",
);

test("procurement browses BOQ forms by sheet and downloads the unchanged file", async ({
  page,
  loginInBrowserAs,
}, testInfo) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.procurement);
  await page.goto("/admin/material-rates/boq", { waitUntil: "networkidle" });

  const tree = page.getByTestId("boq-library-tree");
  await expect(tree.getByTestId("boq-library-template-PCCC-CAN-HO")).toContainText(
    "Serviced-apartment fire protection quotation",
  );
  await expect(tree.getByTestId("boq-library-template-THIET-KE-XIN-PHEP")).toBeVisible();
  // Versions and catalog editing are gone from the BOQ library.
  await expect(page.getByRole("button", { name: /New version|Create version/i })).toHaveCount(0);

  await tree.getByTestId("boq-library-sheet-PCCC-CAN-HO-ptct").click();
  const outline = page.getByTestId("boq-library-outline");
  await expect(outline).toContainText("Trung tâm báo cháy 8 zone");
  await expect(outline).toContainText("CỘNG ( A + B + C)");
  await expect(tree.getByTestId("boq-library-sheet-PCCC-CAN-HO-ptct")).toContainText("78 items");
  await expect(page.locator("body")).not.toContainText("boqLibrary.");

  const downloading = page.waitForEvent("download");
  await page.getByTestId("boq-library-download").click();
  const download = await downloading;
  expect(download.suggestedFilename()).toBe("BOQ-PCCC-Can-ho-dich-vu.xlsx");
  expect((await readFile((await download.path())!)).equals(await readFile(templatePath))).toBe(
    true,
  );

  // The second form expands as its own folder and keeps its own sheet.
  await tree.getByTestId("boq-library-template-THIET-KE-XIN-PHEP").click();
  await tree.getByTestId("boq-library-sheet-THIET-KE-XIN-PHEP-Bao gia").click();
  await expect(outline).toContainText("Thiết kế cơ sở");

  // The factory form keeps all 23 customer sheets; names may hold "&" and a trailing space.
  await tree.getByTestId("boq-library-template-NHA-XUONG").click();
  await expect(tree.getByTestId("boq-library-sheet-NHA-XUONG-00.Tổng Hợp")).toBeVisible();
  await tree.getByTestId("boq-library-sheet-NHA-XUONG-C4.Hệ Thống Điện & Điện Nhẹ ").click();
  await expect(outline).toContainText("Recloser 3P-24kV-630A");

  // The ME base form names PCCC items with their size; measurement rows stay out of the outline.
  await tree.getByTestId("boq-library-template-XAY-DUNG-ME").click();
  await tree.getByTestId("boq-library-sheet-XAY-DUNG-ME-PCCC").click();
  await expect(outline).toContainText("Ống kẽm D168 x 3.96mm");
  await tree.getByTestId("boq-library-sheet-XAY-DUNG-ME-Bể XLNT").click();
  await expect(outline).toContainText("Công tác ván khuôn vách");
  await expect(outline.getByText("vách bao", { exact: true })).toHaveCount(0);

  // The lump-sum form keeps only its visible sheets; electrical items carry no item numbers.
  await tree.getByTestId("boq-library-template-TRON-GOI").click();
  await expect(tree.getByTestId("boq-library-sheet-TRON-GOI-List material")).toHaveCount(0);
  await tree.getByTestId("boq-library-sheet-TRON-GOI-Electric X1").click();
  await expect(outline).toContainText("Máy biến áp 3P-1000kVA");

  // The CSA form reads three bills; the measured rows under each item stay out of the outline.
  await tree.getByTestId("boq-library-template-CSA-WORK").click();
  await tree.getByTestId("boq-library-sheet-CSA-WORK-Bill 2-Main building works").click();
  await expect(outline).toContainText("Excavation of  Soil/ Công tác đào đất");
  await expect(outline.getByText("M4a", { exact: true })).toHaveCount(0);

  for (const [name, width, height] of [
    ["mobile", 390, 844],
    ["desktop", 1440, 900],
  ] as const) {
    await page.setViewportSize({ width, height });
    await expect(outline).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({ path: testInfo.outputPath(`boq-library-${name}.png`), fullPage: true });
  }
});
