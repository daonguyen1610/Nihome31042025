import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/financeReview.ts", import.meta.url), "utf8"),
  {
    compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
  },
).outputText;
const { selectQuoteFinanceReview } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("old approved dossier with newer package metadata cannot replace current quote version", () => {
  const old = {
    id: 1,
    status: "Completed",
    financeReview: { reviewedQuoteVersion: 1, status: "Approved" },
  };
  const current = {
    id: 2,
    status: "Completed",
    financeReview: { reviewedQuoteVersion: 2, status: "Approved" },
  };
  assert.equal(selectQuoteFinanceReview([old, current], 2), current);
  assert.equal(selectQuoteFinanceReview([old], 2), undefined);
});

test("source quote and contract reuse a pending or rejected current dossier instead of older approval", () => {
  for (const status of ["Draft", "Submitted", "Rejected"]) {
    const current = { id: 2, status: "Active", financeReview: { reviewedQuoteVersion: 2, status } };
    const prior = {
      id: 1,
      status: "Completed",
      financeReview: { reviewedQuoteVersion: 1, status: "Approved" },
    };
    assert.equal(selectQuoteFinanceReview([current, prior], 2), current);
  }
});

test("cancelled and obsolete dossiers require source quote fallback; unfinished new dossier remains reusable", () => {
  const cancelled = {
    status: "Cancelled",
    financeReview: { reviewedQuoteVersion: 2, status: "Approved" },
  };
  assert.equal(selectQuoteFinanceReview([cancelled], 2), undefined);
  const draft = { status: "Planned", financeReview: null };
  assert.equal(selectQuoteFinanceReview([cancelled, draft], 2), draft);
  assert.equal(selectQuoteFinanceReview([], 2), undefined);
});
