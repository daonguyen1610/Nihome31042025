import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const load = async (file) => {
  const compiled = ts.transpileModule(
    await readFile(new URL(`../src/lib/${file}`, import.meta.url), "utf8"),
    { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
  ).outputText;
  return import("data:text/javascript;base64," + Buffer.from(compiled).toString("base64"));
};
const { createUploadTracker } = await load("uploadTracker.ts");
const { uploadInChunks } = await load("chunkedUpload.ts");

const file = (size) =>
  Object.assign(new Blob([new Uint8Array(size)]), { name: "hop-dong-thi-cong.pdf" });
const dropped = Object.assign(new Error("Network Error"), { code: "ERR_NETWORK" });
const options = {
  isConnectionLost: (error) => error === dropped,
  receivedBytesOf: (error) => error?.receivedBytes ?? null,
  wait: async (_ms, signal) => {
    if (signal.aborted) throw new Error("aborted");
  },
};

/** A server that holds bytes and can drop the connection part-way through a chunk. */
function fakeServer({ chunkSize = 10, dropAt = [] } = {}) {
  const calls = [];
  let held = 0;
  let cancelled = false;
  return {
    calls,
    get held() {
      return held;
    },
    get cancelled() {
      return cancelled;
    },
    transport: {
      start: async () => ({ uploadId: "u1", chunkSize }),
      send: async (_id, offset, chunk, onProgress) => {
        calls.push(offset);
        if (offset !== held) throw { receivedBytes: held };
        const drop = dropAt.shift();
        if (drop !== undefined) {
          held += drop;
          onProgress(drop);
          throw dropped;
        }
        held += chunk.size;
        onProgress(chunk.size);
        return held;
      },
      complete: async () => ({ id: 7, fileSize: held }),
      cancel: async () => {
        cancelled = true;
      },
    },
  };
}

test("a file is sent chunk by chunk and finishes in the tray", async () => {
  const tracker = createUploadTracker();
  const server = fakeServer();
  const result = await uploadInChunks(file(25), tracker, server.transport, options);
  assert.deepEqual(server.calls, [0, 10, 20]);
  assert.deepEqual(result, { id: 7, fileSize: 25 });
  assert.equal(tracker.getSnapshot()[0].status, "done");
});

test("after the connection drops the upload retries and the tray says so", async () => {
  const tracker = createUploadTracker();
  // The second chunk is lost before any of it arrives.
  const server = fakeServer({ dropAt: [undefined, 0] });
  const seen = [];
  tracker.subscribe(() => seen.push(tracker.getSnapshot()[0]?.reconnecting === true));
  await uploadInChunks(file(25), tracker, server.transport, options);
  assert.deepEqual(server.calls, [0, 10, 10, 20]);
  assert.equal(server.held, 25, "every byte arrives once");
  assert.ok(seen.includes(true), "the tray says the connection was lost");
  assert.equal(tracker.getSnapshot()[0].status, "done");
  assert.equal(tracker.getSnapshot()[0].reconnecting, false);
});

test("a chunk cut part-way resumes at the server's count, not the chunk start", async () => {
  const tracker = createUploadTracker();
  const server = fakeServer({ dropAt: [4] });
  await uploadInChunks(file(25), tracker, server.transport, options);
  // 0 (drops after 4 bytes) → retry 0 is refused with 4 → 4 → 14 → 24.
  assert.deepEqual(server.calls, [0, 0, 4, 14, 24]);
  assert.equal(server.held, 25);
});

test("a cancel stops the upload, discards it on the server and is not a failure", async () => {
  const tracker = createUploadTracker();
  const server = fakeServer();
  server.transport.send = async () => {
    tracker.cancel(tracker.getSnapshot()[0].id);
    throw Object.assign(new Error("canceled"), { code: "ERR_CANCELED" });
  };
  await assert.rejects(uploadInChunks(file(25), tracker, server.transport, options), {
    code: "ERR_CANCELED",
  });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(server.cancelled, true);
  assert.equal(tracker.getSnapshot()[0].status, "cancelled");
});

test("a refusal from the server fails the upload with its reason", async () => {
  const tracker = createUploadTracker();
  const server = fakeServer();
  const refused = { response: { status: 400, data: { message: "too large" } } };
  server.transport.send = async () => {
    throw refused;
  };
  await assert.rejects(uploadInChunks(file(25), tracker, server.transport, options));
  assert.equal(tracker.getSnapshot()[0].status, "failed");
  assert.equal(tracker.getSnapshot()[0].error, refused);
});
