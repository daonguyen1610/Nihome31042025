import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

// Strip the React/axios imports so the date helpers can run under node:test.
const source = (
  await readFile(new URL("../src/lib/displaySettings.ts", import.meta.url), "utf8")
).replace(/^import .*;\n/gm, "");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
globalThis.setActiveCurrency = () => {};
const {
  applyDisplaySettings,
  dateTimeLocalToIso,
  formatDate,
  getDisplaySettings,
  toDateTimeLocalValue,
} = await import("data:text/javascript;base64," + Buffer.from(compiled).toString("base64"));

test("datetime-local values are interpreted in the configured zone, not the browser's", () => {
  applyDisplaySettings({ timeZoneId: "Asia/Ho_Chi_Minh", currency: "VND" });
  assert.equal(dateTimeLocalToIso("2026-03-02T00:00"), "2026-03-01T17:00:00.000Z");
  assert.equal(toDateTimeLocalValue("2026-03-01T17:00:00.000Z"), "2026-03-02T00:00");
  applyDisplaySettings({ timeZoneId: "America/New_York", currency: "VND" });
  assert.equal(dateTimeLocalToIso("2026-07-01T09:30"), "2026-07-01T13:30:00.000Z");
  assert.equal(toDateTimeLocalValue("2026-07-01T13:30:00Z"), "2026-07-01T09:30");
  applyDisplaySettings({ timeZoneId: "Asia/Ho_Chi_Minh", currency: "VND" });
});

test("an unusable configured zone falls back to the Vietnam default", () => {
  applyDisplaySettings({ timeZoneId: "Not/AZone", currency: "USD" });
  assert.equal(getDisplaySettings().timeZoneId, "Asia/Ho_Chi_Minh");
  assert.equal(dateTimeLocalToIso("2026-03-02T00:00"), "2026-03-01T17:00:00.000Z");
  applyDisplaySettings({ timeZoneId: "Asia/Ho_Chi_Minh", currency: "VND" });
});

test("field options replace the helper defaults; zone-only options keep them", () => {
  assert.equal(formatDate("2026-03-02", "en", { month: "short", timeZone: "UTC" }), "Mar");
  assert.equal(formatDate("2026-03-02", "en", { timeZone: "UTC" }), "3/2/2026");
  assert.equal(
    formatDate("2026-03-02", "en", { dateStyle: "medium", timeZone: "UTC" }),
    "Mar 2, 2026",
  );
});

test("invalid datetime-local text is rejected instead of silently shifted", () => {
  assert.equal(dateTimeLocalToIso(""), null);
  assert.equal(dateTimeLocalToIso("2026-03-02"), null);
  assert.equal(dateTimeLocalToIso("not a date"), null);
});
