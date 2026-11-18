import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/surveyStatus.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { surveyProgress, surveyTimeLabel } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const TODAY = "2026-11-20";

test("a recorded survey is surveyed whatever its date", () => {
  assert.equal(
    surveyProgress({ status: "Completed", surveyDate: "2026-11-01T00:00:00Z" }, TODAY),
    "completed",
  );
});

test("a planned survey is overdue only once its day has passed", () => {
  assert.equal(
    surveyProgress({ status: "Planned", surveyDate: "2026-11-19T00:00:00Z" }, TODAY),
    "overdue",
  );
  assert.equal(
    surveyProgress({ status: "Planned", surveyDate: "2026-11-20T00:00:00Z" }, TODAY),
    "planned",
  );
  assert.equal(
    surveyProgress({ status: "Planned", surveyDate: "2026-12-01T00:00:00Z" }, TODAY),
    "planned",
  );
});

test("the time is shown as HH:mm and is optional", () => {
  assert.equal(surveyTimeLabel("08:30:00"), "08:30");
  assert.equal(surveyTimeLabel(null), null);
  assert.equal(surveyTimeLabel(undefined), null);
});
