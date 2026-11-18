import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/searchMatch.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { matchesAllTerms } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("a tax ID matches only the customer that has it", () => {
  assert.equal(matchesAllTerms("Công ty May An Phú Mã số thuế: 0100000001", "0100000001"), true);
  // Scattered digits in the right order are not a match.
  assert.equal(matchesAllTerms("Công ty Tân Phúc Mã số thuế: 0100000003", "0000000000"), false);
});

test("Vietnamese text matches with or without accents and case", () => {
  assert.equal(matchesAllTerms("Công ty Cổ phần Gia dụng Tân Phúc", "tan phuc"), true);
  assert.equal(matchesAllTerms("Đặng Văn Đức", "dang duc"), true);
  assert.equal(matchesAllTerms("Nhà máy may An Phú", "NHÀ MÁY"), true);
});

test("every word typed must appear, in any order", () => {
  assert.equal(
    matchesAllTerms("PJ-SAMPLE-005 · Trụ sở Gia dụng Tân Phúc", "tân pj-sample-005"),
    true,
  );
  assert.equal(matchesAllTerms("PJ-SAMPLE-005 · Trụ sở Gia dụng Tân Phúc", "tân bình"), false);
});

test("an empty search matches everything", () => {
  assert.equal(matchesAllTerms("anything", "   "), true);
});
