import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/lib/numberFormat.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { parseVndDecimal } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("localized price quantities preserve three- and four-place decimal commas", () => {
  for (const [text, expected] of [
    ["0,001", 0.001],
    ["0,0001", 0.0001],
    ["12,345", 12.345],
    ["12.500,1234", 12500.1234],
  ]) {
    assert.equal(parseVndDecimal(text, 4, true), expected);
  }
});

test("localized price quantities preserve grouped integers and reject invalid precision", () => {
  assert.equal(parseVndDecimal("12.500", 4, true), 12500);
  assert.equal(parseVndDecimal("0", 4, true), 0);
  assert.equal(parseVndDecimal("", 4, true), 0);
  for (const text of ["0,00001", "not a number", "1.23.456", "-1"]) {
    assert.ok(Number.isNaN(parseVndDecimal(text, 4, true)), text);
  }
});

test("existing currency fields retain their separator interpretation", () => {
  assert.equal(parseVndDecimal("85.000"), 85000);
  assert.equal(parseVndDecimal("85,000"), 85000);
  assert.equal(parseVndDecimal("85.000,50"), 85000.5);
  assert.equal(parseVndDecimal("85,000.50"), 85000.5);
});
