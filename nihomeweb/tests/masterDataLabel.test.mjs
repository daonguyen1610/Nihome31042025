import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/masterDataLabel.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { masterDataLabel } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

// The app's t() returns the key itself when no translation exists.
const t = (key) =>
  ({ "masterData.business_sector.food-beverage.label": "Food & beverage" })[key] ?? key;

test("a seeded option reads in the user's language", () => {
  assert.equal(
    masterDataLabel(t, {
      labelKey: "masterData.business_sector.food-beverage.label",
      name: "Thực phẩm & Đồ uống",
    }),
    "Food & beverage",
  );
});

test("an option an admin added (no translation) reads by its name, then its code", () => {
  assert.equal(masterDataLabel(t, { labelKey: null, name: "Kho vận" }), "Kho vận");
  assert.equal(
    masterDataLabel(t, { labelKey: "masterData.business_sector.new.label", name: "Kho vận" }),
    "Kho vận",
  );
  assert.equal(masterDataLabel(t, { name: "", code: "warehouse" }), "warehouse");
});
