import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/newVersionReload.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { isStaleBundleError, reloadOnceForNewVersion, shouldReloadForPreloadError } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const memory = () => {
  const values = new Map();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
  };
};

test("recognises the errors browsers raise for a bundle file removed by a deploy", () => {
  for (const message of [
    "Failed to fetch dynamically imported module: http://niconvn.com/assets/QuoteDetail-OLD.js",
    "Importing a module script failed.",
    "error loading dynamically imported module",
    "'text/html' is not a valid JavaScript MIME type.",
    "Unable to preload CSS for /assets/index-OLD.css",
  ])
    assert.equal(isStaleBundleError(new Error(message)), true, message);
  assert.equal(isStaleBundleError(new Error("Cannot read properties of undefined")), false);
  assert.equal(isStaleBundleError(undefined), false);
});

test("reloads once to fetch the new version, never in a loop", () => {
  const storage = memory();
  let reloads = 0;
  const reload = () => reloads++;
  assert.equal(reloadOnceForNewVersion(storage, 1_000_000, reload), true);
  // Still broken right after reloading: show the error instead of reloading forever.
  assert.equal(reloadOnceForNewVersion(storage, 1_030_000, reload), false);
  assert.equal(reloads, 1);
  // A later deploy may reload again.
  assert.equal(reloadOnceForNewVersion(storage, 1_200_000, reload), true);
  assert.equal(reloads, 2);
});

test("without storage (private mode) it reloads at most once per page", () => {
  const broken = {
    getItem: () => {
      throw new Error("blocked");
    },
    setItem: () => {
      throw new Error("blocked");
    },
  };
  let reloads = 0;
  assert.equal(
    reloadOnceForNewVersion(broken, 1, () => reloads++),
    true,
  );
  assert.equal(
    reloadOnceForNewVersion(broken, 2, () => reloads++),
    false,
  );
  assert.equal(reloads, 1);
});

test("a preload error from a page's own work, such as a broken PDF, never reloads the page", () => {
  // Vite reports errors raised after a dynamic import (here pdf.js parsing a
  // file) as preload errors; only a missing bundle file means a new version.
  const invalidPdf = Object.assign(new Error("Invalid PDF structure."), {
    name: "InvalidPDFException",
  });
  assert.equal(shouldReloadForPreloadError(invalidPdf), false);
  assert.equal(shouldReloadForPreloadError(undefined), false);
  assert.equal(
    shouldReloadForPreloadError(
      new TypeError("Failed to fetch dynamically imported module: https://niconvn.com/assets/X.js"),
    ),
    true,
  );
});
