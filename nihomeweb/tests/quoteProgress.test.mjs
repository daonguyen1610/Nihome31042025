import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/quoteProgress.ts", import.meta.url), "utf8"),
  {
    compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
  },
).outputText;
const { getQuoteProgress } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const step = (status, grandTotal = 120_000_000, financeApproved = false) =>
  getQuoteProgress({ status, grandTotal, financeApproved });

test("an unpriced draft waits for the procurement input price", () => {
  assert.deepEqual(step("Draft", 0), { kind: "active", step: "input", index: 0 });
});

test("a priced draft waits for sales to finish and submit the output quote", () => {
  assert.equal(step("Draft").step, "output");
});

test("internal approval, finance review and sending follow in order", () => {
  assert.equal(step("PendingApproval").step, "internal");
  assert.equal(step("Approved", 120_000_000, false).step, "finance");
  assert.equal(step("Approved", 120_000_000, true).step, "send");
  assert.equal(step("SentToCustomer").step, "customer");
});

test("customer approval completes the flow; other end states stop it", () => {
  assert.deepEqual(step("CustomerApproved"), { kind: "done" });
  for (const status of ["Rejected", "Expired", "Cancelled"])
    assert.deepEqual(step(status), { kind: "stopped" });
});
