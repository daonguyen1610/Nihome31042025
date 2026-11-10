import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(
  new URL("../src/pages/admin/construction/constructionProjectSelection.ts", import.meta.url),
  "utf8",
);
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { selectAccessibleProjectId, selectDesignProjectId } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("a stale operational-project URL falls back to an accessible project", () => {
  assert.equal(selectAccessibleProjectId(7128, [{ id: 24 }, { id: 31 }]), 24);
});

test("an accessible operational-project URL remains selected", () => {
  assert.equal(selectAccessibleProjectId(31, [{ id: 24 }, { id: 31 }]), 31);
});

test("a design project from another operational project is rejected", () => {
  assert.equal(selectDesignProjectId(900, 42, [{ id: 41 }, { id: 42 }]), 42);
});

test("design-project selection falls back to the first project when the default is stale", () => {
  assert.equal(selectDesignProjectId(900, 800, [{ id: 41 }, { id: 42 }]), 41);
});
