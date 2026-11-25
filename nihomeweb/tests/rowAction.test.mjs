import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

// Strip the clsx/tailwind imports so rowAction can run under node:test.
const source = (await readFile(new URL("../src/lib/utils.ts", import.meta.url), "utf8")).replace(
  /^import .*;\n/gm,
  "",
);
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { rowAction } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

function press(props, key, fromInside = false) {
  const row = {};
  let prevented = false;
  props.onKeyDown({
    key,
    currentTarget: row,
    target: fromInside ? {} : row,
    preventDefault: () => (prevented = true),
  });
  return prevented;
}

test("Enter or Space on the row opens it", () => {
  let opened = 0;
  const props = rowAction(() => opened++);
  assert.equal(props.tabIndex, 0);
  assert.equal(press(props, "Enter"), true);
  assert.equal(press(props, " "), true);
  assert.equal(opened, 2);
});

test("keys from a button inside the row, and other keys, do not open it", () => {
  let opened = 0;
  const props = rowAction(() => opened++);
  assert.equal(press(props, "Enter", true), false);
  assert.equal(press(props, "Tab"), false);
  assert.equal(opened, 0);
});
