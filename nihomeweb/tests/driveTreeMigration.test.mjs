import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/driveTreeMigration.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { driveTreeActions } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const actions = (status, undoStarted = false, blockers = []) =>
  driveTreeActions({
    blockers,
    latestMigration: status ? { id: 1, status, undoStarted, error: null } : null,
  });
const offered = ({ forward, undo }) => [forward, undo];

test("a tree that was never moved, or was rolled back, can only be started", () => {
  assert.deepEqual(offered(actions(null)), ["start", false]);
  assert.deepEqual(offered(actions("RolledBack")), ["start", false]);
  assert.deepEqual(offered(actions("RolledBack", true)), ["start", false]);
});

test("a completed move can only be undone", () => {
  assert.deepEqual(offered(actions("Completed")), [null, true]);
});

test("a stopped move can be resumed, and undone only once it has failed", () => {
  assert.deepEqual(offered(actions("Failed")), ["resume", true]);
  assert.deepEqual(offered(actions("Running")), ["resume", false]);
});

test("a half-done undo offers nothing but finishing the undo", () => {
  for (const status of ["Running", "Completed", "Failed"])
    assert.deepEqual(offered(actions(status, true)), [null, true]);
});

test("an unknown status offers no action", () => {
  assert.deepEqual(offered(actions("Paused")), [null, false]);
});

test("each action keeps the blockers the server would refuse it for", () => {
  const all = [
    "DRIVE_SYNC_ENABLED",
    "DRIVE_DOCUMENTS_PROCESSING",
    "DRIVE_NOT_CONNECTED",
    "DRIVE_TREE_ALREADY_STANDARD",
    "DRIVE_TREE_MIGRATION_RUNNING",
    "DRIVE_TREE_UNDO_IN_PROGRESS",
  ];
  const environment = all.slice(0, 3);
  assert.deepEqual(actions(null, false, all).forwardBlockers, all);
  // Resuming ignores the two blockers that the stopped run itself causes, as the server does.
  assert.deepEqual(actions("Running", false, all).forwardBlockers, [
    ...environment,
    "DRIVE_TREE_UNDO_IN_PROGRESS",
  ]);
  assert.deepEqual(actions("Failed", false, all).undoBlockers, environment);
  // Blockers of actions that are not offered are not shown.
  assert.deepEqual(actions("Completed", false, all).forwardBlockers, []);
  assert.deepEqual(actions(null, false, all).undoBlockers, []);
});
