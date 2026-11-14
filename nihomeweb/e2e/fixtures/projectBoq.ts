import { randomUUID } from "node:crypto";
import JSZip from "jszip";
import { expect, type APIRequestContext } from "@playwright/test";

export const xlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
const sheetPart = "xl/worksheets/sheet1.xml";

export type Cells = Record<string, string | number>;

// Writes values the way Excel saves them: typed cells plus stored formula results.
export async function fillWorkbook(template: Buffer, cells: Cells, part = sheetPart) {
  const zip = await JSZip.loadAsync(template);
  let xml = await zip.file(part)!.async("string");
  for (const [ref, value] of Object.entries(cells)) {
    const pattern = new RegExp(`<c r="${ref}"([^>]*?)(/>|>([\\s\\S]*?)</c>)`);
    const match = pattern.exec(xml);
    expect(match, `template cell ${ref}`).not.toBeNull();
    const attributes = match![1].replace(/\s*t="[^"]*"/, "");
    const formula = /<f[\s\S]*?(?:<\/f>|\/>)/.exec(match![3] ?? "")?.[0] ?? "";
    xml = xml.replace(
      pattern,
      typeof value === "number"
        ? `<c r="${ref}"${attributes}>${formula}<v>${value}</v></c>`
        : `<c r="${ref}"${attributes} t="inlineStr"><is><t>${value}</t></is></c>`,
    );
  }
  zip.file(part, xml);
  return zip.generateAsync({ type: "nodebuffer" });
}

export interface ProjectBoqLine {
  name: string;
  unit: string;
  quantity: number;
  unitPrice: number;
}

/**
 * Uploads the serviced-apartment BOQ form with these lines in group A (rows 16–29), then
 * submits and approves it as the project's tender BOQ — the only source a BOQ quote accepts.
 * Lines are named S01-R0016, S01-R0017, … on the quote.
 */
export async function approveProjectBoq(
  api: APIRequestContext,
  headers: Record<string, string>,
  projectId: number,
  lines: ProjectBoqLine[],
): Promise<{ id: number; total: number }> {
  expect(lines.length).toBeLessThanOrEqual(14);
  const base = `/api/operational-projects/${projectId}/procurement`;
  const templateResponse = await api.get(`${base}/boq-templates/PCCC-CAN-HO/file`, { headers });
  expect(templateResponse.status(), await templateResponse.text()).toBe(200);
  const cells: Cells = {};
  let total = 0;
  lines.forEach((line, index) => {
    const row = 16 + index;
    const amount = line.quantity * line.unitPrice;
    Object.assign(cells, {
      [`B${row}`]: line.name,
      [`D${row}`]: line.unit,
      [`E${row}`]: line.quantity,
      [`F${row}`]: line.unitPrice,
      [`G${row}`]: amount,
    });
    total += amount;
  });
  Object.assign(cells, { G30: total, G101: total });
  const file = {
    name: `boq-du-an-${projectId}.xlsx`,
    mimeType: xlsxType,
    buffer: await fillWorkbook(await templateResponse.body(), cells),
  };

  const preview = await api.post(`${base}/boq-workbooks/preview`, {
    headers,
    multipart: { templateCode: "PCCC-CAN-HO", file },
  });
  expect(preview.status(), await preview.text()).toBe(200);
  const created = await api.post(`${base}/boq-workbooks`, {
    headers,
    multipart: {
      templateCode: "PCCC-CAN-HO",
      sourceHash: (await preview.json()).sourceHash as string,
      file,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const revision = await created.json();
  const write = (path: string, data: object) =>
    api.post(`${base}/boq-revisions/${revision.id}/${path}`, {
      headers: { ...headers, "Idempotency-Key": randomUUID() },
      data,
    });
  const submitted = await write("submit", { rowVersion: revision.rowVersion });
  expect(submitted.status(), await submitted.text()).toBe(200);
  const approved = await write("decision", {
    approved: true,
    rowVersion: (await submitted.json()).rowVersion,
  });
  expect(approved.status(), await approved.text()).toBe(200);
  expect((await approved.json()).status).toBe("TenderApproved");
  return { id: revision.id as number, total };
}
