import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/uploadTracker.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { createUploadTracker } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("an upload reports progress and finishes", () => {
  const tracker = createUploadTracker();
  const seen = [];
  tracker.subscribe(() => seen.push(tracker.getSnapshot().length));
  const id = tracker.start({ name: "ban-ve.pdf", size: 1000, abort: () => {} });
  tracker.progress(id, 400);
  assert.equal(tracker.getSnapshot()[0].loaded, 400);
  assert.equal(tracker.activeCount(), 1);
  tracker.finish(id);
  assert.equal(tracker.getSnapshot()[0].status, "done");
  assert.equal(tracker.getSnapshot()[0].loaded, 1000);
  assert.equal(tracker.activeCount(), 0);
  assert.ok(seen.length >= 3, "subscribers hear every change");
});

test("cancelling aborts the request and is not counted as a failure", () => {
  const tracker = createUploadTracker();
  let aborted = false;
  const id = tracker.start({ name: "hop-dong.pdf", size: 10, abort: () => (aborted = true) });
  tracker.cancel(id);
  assert.equal(aborted, true);
  assert.equal(tracker.getSnapshot()[0].status, "cancelled");
  // The request then rejects; that must not turn the cancel into a failure.
  tracker.fail(id, new Error("canceled"));
  assert.equal(tracker.getSnapshot()[0].status, "cancelled");
});

test("a failure keeps its reason, and finished rows can be cleared", () => {
  const tracker = createUploadTracker();
  const ok = tracker.start({ name: "a.pdf", size: 1, abort: () => {} });
  const bad = tracker.start({ name: "b.pdf", size: 1, abort: () => {} });
  const running = tracker.start({ name: "c.pdf", size: 1, abort: () => {} });
  tracker.finish(ok);
  const reason = new Error("server said no");
  tracker.fail(bad, reason);
  assert.equal(tracker.getSnapshot().find((item) => item.id === bad).error, reason);
  tracker.clearFinished();
  assert.deepEqual(
    tracker.getSnapshot().map((item) => item.id),
    [running],
  );
});
