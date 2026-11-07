import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import ts from "typescript";
async function files(dir) {
  const entries = await readdir(dir, { withFileTypes: true });
  return (
    await Promise.all(
      entries.map((entry) =>
        entry.isDirectory() ? files(path.join(dir, entry.name)) : [path.join(dir, entry.name)],
      ),
    )
  ).flat();
}
const sources = await files(fileURLToPath(new URL("../src", import.meta.url)));
test("UI uses application confirmations and protects new browsing contexts", async () => {
  const failures = [];
  for (const file of sources.filter((file) => /\.tsx?$/.test(file))) {
    const text = await readFile(file, "utf8");
    const sf = ts.createSourceFile(file, text, 99, true, ts.ScriptKind.TSX);
    function visit(node) {
      if (ts.isCallExpression(node) && node.expression.getText(sf) === "window.confirm")
        failures.push(`${file}: native confirmation`);
      if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
        const attrs = node.attributes.properties.filter(ts.isJsxAttribute);
        const target = attrs.find((a) => a.name.getText(sf) === "target");
        if (
          target?.initializer &&
          ts.isStringLiteral(target.initializer) &&
          target.initializer.text === "_blank"
        ) {
          const rel = attrs.find((a) => a.name.getText(sf) === "rel")?.initializer;
          if (
            !rel ||
            !ts.isStringLiteral(rel) ||
            !["noopener", "noreferrer"].every((value) => rel.text.split(/\s+/).includes(value))
          )
            failures.push(`${file}: unsafe new browsing context`);
        }
      }
      ts.forEachChild(node, visit);
    }
    visit(sf);
  }
  assert.deepEqual(failures, []);
});
