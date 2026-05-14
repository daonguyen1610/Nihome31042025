import { describe, expect, it } from "vitest";
import { buildExcelCsv, createCsvFilename, CSV_UTF8_BOM } from "@/lib/exportCsv";

describe("exportCsv", () => {
  it("builds Excel-compatible CSV with UTF-8 BOM and escaped cells", () => {
    const csv = buildExcelCsv({
      columns: [
        { header: "Name", value: "name" },
        { header: "Note", value: "note" },
        { header: "Empty", value: "empty" },
        { header: "Missing", value: "missing" },
      ],
      rows: [
        {
          name: 'NICON, "Design"',
          note: "Line 1\nLine 2",
          empty: null,
          missing: undefined,
        },
      ],
    });

    expect(csv.startsWith(CSV_UTF8_BOM)).toBe(true);
    expect(csv).toContain("Name,Note,Empty,Missing");
    expect(csv).toContain('"NICON, ""Design""","Line 1\nLine 2",,');
  });

  it("uses safe filenames with a date stamp", () => {
    expect(createCsvFilename("Admin Contacts", new Date("2026-05-07T10:00:00Z"))).toBe(
      "admin-contacts-2026-05-07.csv",
    );
  });

  it("prefixes formula-like values to reduce spreadsheet injection risk", () => {
    const csv = buildExcelCsv({
      columns: [{ header: "Value", value: "value" }],
      rows: [{ value: "=HYPERLINK(\"https://example.com\")" }],
    });

    expect(csv).toContain('\'=HYPERLINK(""https://example.com"")');
  });
});
