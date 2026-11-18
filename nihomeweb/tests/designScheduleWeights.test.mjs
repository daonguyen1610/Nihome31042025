import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/designScheduleWeights.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { taskWeightTotals, suggestedTaskWeight } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const source = (phaseId, weights) => ({
  phaseId,
  weight: 34,
  baselineReady: false,
  taskSources: weights.map((weight, index) => ({
    taskId: index + 1,
    weight,
    progressPercent: 0,
    weightedValue: 0,
  })),
});

test("sums the task weights of every phase", () => {
  const totals = taskWeightTotals([source(1, [40, 25]), source(2, [])]);
  assert.equal(totals.get(1), 65);
  assert.equal(totals.get(2), 0);
});

test("suggests the weight that completes the phase to 100%", () => {
  assert.equal(suggestedTaskWeight(65), 35);
  assert.equal(suggestedTaskWeight(0), 100);
});

test("never suggests less than 1% once a phase is full or over", () => {
  assert.equal(suggestedTaskWeight(100), 1);
  assert.equal(suggestedTaskWeight(130), 1);
});
