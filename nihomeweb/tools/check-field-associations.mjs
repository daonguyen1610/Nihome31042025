import ts from "typescript";
import fs from "node:fs";
import path from "node:path";

const sourceRoot = path.resolve("src");
const failures = [];
const counts = {
  explicit: 0,
  scoped: 0,
  implicit: 0,
  namedControls: 0,
  hiddenAdapters: 0,
  forwardedControls: 0,
};
const controls = /<(?:Input|Textarea|input|textarea|select|Checkbox|Switch)\b/;
function inspect(file) {
  const source = ts.createSourceFile(
    file,
    fs.readFileSync(file, "utf8"),
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TSX,
  );
  const labelTargets = new Set();
  const linkedHelpers = new Set();
  const allNodes = [];
  function collect(node) {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      allNodes.push(node);
      const target = node.attributes.properties.find(
        (attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(source) === "htmlFor",
      );
      if (target?.initializer) labelTargets.add(target.initializer.getText(source));
    }
    ts.forEachChild(node, collect);
  }
  collect(source);
  for (const node of allNodes) {
    const id = node.attributes.properties.find(
      (attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(source) === "id",
    );
    if (id?.initializer && labelTargets.has(id.initializer.getText(source)))
      linkedHelpers.add(node.tagName.getText(source));
  }
  function visit(node) {
    if (
      !file.includes(`${path.sep}components${path.sep}ui${path.sep}`) &&
      (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) &&
      [
        "Input",
        "Textarea",
        "MoneyInput",
        "SearchableSelect",
        "SelectTrigger",
        "input",
        "textarea",
        "select",
      ].includes(node.tagName.getText(source))
    ) {
      const attributes = node.attributes.properties;
      const attr = (name) =>
        attributes.find(
          (attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(source) === name,
        );
      const kind = attr("type")?.initializer?.getText(source);
      const hidden =
        kind === '"hidden"' ||
        /\bhidden\b/.test(attr("className")?.initializer?.getText(source) ?? "");
      const spread = attributes.some(ts.isJsxSpreadAttribute);
      let ancestor = node.parent;
      let associated = false;
      let forwarded = false;
      while (ancestor) {
        if (
          ts.isJsxElement(ancestor) &&
          [
            "FieldScope",
            "Field",
            "FieldRow",
            "FilterField",
            "FormField",
            "RfqField",
            "label",
            "Label",
          ].includes(ancestor.openingElement.tagName.getText(source))
        )
          associated = true;
        if (
          ts.isFunctionDeclaration(ancestor) &&
          ancestor.name &&
          linkedHelpers.has(ancestor.name.text)
        )
          forwarded = true;
        ancestor = ancestor.parent;
      }
      const id = attr("id")?.initializer?.getText(source);
      const named =
        attr("aria-label") ||
        attr("aria-labelledby") ||
        attr("ariaLabel") ||
        attr("title") ||
        (id && labelTargets.has(id));
      if (hidden) counts.hiddenAdapters++;
      else if (forwarded || spread) counts.forwardedControls++;
      else if (named || associated || kind === '"checkbox"' || kind === '"radio"')
        counts.namedControls++;
      else
        failures.push(
          `${path.relative(process.cwd(), file)}:${source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1}: ${node.tagName.getText(source)} needs a persistent field label or an explicit accessible name.`,
        );
    }

    if (ts.isJsxElement(node) && node.openingElement.tagName.getText(source) === "Label") {
      const explicit = node.openingElement.attributes.properties.some(
        (attribute) => ts.isJsxAttribute(attribute) && attribute.name.getText(source) === "htmlFor",
      );
      let ancestor = node.parent;
      let scoped = false;
      while (ancestor) {
        if (
          ts.isJsxElement(ancestor) &&
          ancestor.openingElement.tagName.getText(source) === "FieldScope"
        ) {
          scoped = true;
          break;
        }
        ancestor = ancestor.parent;
      }
      if (explicit) counts.explicit++;
      else if (scoped) counts.scoped++;
      else if (controls.test(node.getText(source))) counts.implicit++;
      else
        failures.push(
          `${path.relative(process.cwd(), file)}:${source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1}: Label needs htmlFor, a single-control FieldScope, or an implicitly wrapped control; use text/headings for read-only descriptions.`,
        );
    }
    ts.forEachChild(node, visit);
  }
  visit(source);
}
function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(file);
    else if (file.endsWith(".tsx")) inspect(file);
  }
}
walk(sourceRoot);
console.log(JSON.stringify({ ...counts, failures: failures.length }));
if (failures.length) {
  console.error(failures.join("\n"));
  process.exitCode = 1;
}
