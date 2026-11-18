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

const step = (
  status,
  grandTotal = 120_000_000,
  financeApproved = false,
  priceSourceReady = false,
) => getQuoteProgress({ status, grandTotal, financeApproved, priceSourceReady });

test("an unpriced draft without a price source waits for the procurement input price", () => {
  assert.deepEqual(step("Draft", 0), { kind: "active", step: "input", index: 0, ready: false });
});

test("an unpriced draft with an approved input price or project BOQ waits for sales to price it", () => {
  assert.deepEqual(step("Draft", 0, false, true), {
    kind: "active",
    step: "output",
    index: 1,
    ready: false,
  });
});

test("a priced draft is ready for sales to submit", () => {
  assert.deepEqual(step("Draft"), { kind: "active", step: "output", index: 1, ready: true });
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
