import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/randomId.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { randomId } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

test("an id is made on a page served over plain http, where randomUUID is missing", () => {
  const native = globalThis.crypto.randomUUID;
  // Browsers only expose randomUUID in secure contexts (https, localhost).
  Object.defineProperty(globalThis.crypto, "randomUUID", { value: undefined, configurable: true });
  try {
    const ids = new Set(Array.from({ length: 1000 }, () => randomId()));
    assert.equal(ids.size, 1000, "ids are unique");
    for (const id of ids) assert.match(id, UUID_V4);
  } finally {
    Object.defineProperty(globalThis.crypto, "randomUUID", { value: native, configurable: true });
  }
});

test("the native generator is used where the browser offers it", () => {
  assert.match(randomId(), UUID_V4);
});
