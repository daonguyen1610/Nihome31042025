import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/designFileRules.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { designFileProblem, DESIGN_FILE_ACCEPT } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const MB = 1024 * 1024;

test("an allowed file within 20 MB passes, whatever the case of its extension", () => {
  assert.equal(designFileProblem({ name: "KT-01.PDF", size: 5 * MB }, 20), null);
});

test("a file over the configured limit reports its size in MB", () => {
  assert.deepEqual(designFileProblem({ name: "KC.pdf", size: 25.3 * MB }, 20), {
    code: "tooLarge",
    sizeMb: 25.3,
  });
  // The same file fits once an administrator raises the limit.
  assert.equal(designFileProblem({ name: "KC.pdf", size: 25.3 * MB }, 50), null);
});

test("without a known limit only the type and emptiness are checked", () => {
  assert.equal(designFileProblem({ name: "KC.pdf", size: 500 * MB }, null), null);
});

test("an empty file and an unsupported type are refused", () => {
  assert.deepEqual(designFileProblem({ name: "a.docx", size: 0 }, 20), { code: "empty" });
  assert.deepEqual(designFileProblem({ name: "ban-ve.DWG", size: 10 }, 20), {
    code: "unsupported",
    extension: ".dwg",
  });
  assert.deepEqual(designFileProblem({ name: "README", size: 10 }, 20), {
    code: "unsupported",
    extension: "",
  });
});

test("the picker accepts the same types the server does", () => {
  assert.equal(DESIGN_FILE_ACCEPT, ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg");
});
