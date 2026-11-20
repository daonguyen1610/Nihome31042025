import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const compiled = ts.transpileModule(
  await readFile(new URL("../src/lib/chunkedUpload.ts", import.meta.url), "utf8"),
  { compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 } },
).outputText;
const { uploadInChunks, chunkedResumeKey, ChunkedUploadError } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const memoryStore = () => {
  const values = new Map();
  return {
    values,
    get: (key) => values.get(key) ?? null,
    set: (key, value) => values.set(key, value),
    remove: (key) => values.delete(key),
  };
};

/** A fake server: stores bytes in order, can drop a chunk's reply or forget a session. */
const fakeServer = ({ chunkSize = 4, failOnce = new Set(), existing = null } = {}) => {
  const sessions = new Map(existing ? [[existing.id, { received: existing.received }]] : []);
  const calls = [];
  let next = 1;
  return {
    calls,
    sessions,
    transport: {
      async start() {
        const id = `s${next++}`;
        sessions.set(id, { received: 0 });
        return { id, receivedBytes: 0, chunkSize };
      },
      async status(id) {
        const session = sessions.get(id);
        return session ? { receivedBytes: session.received, chunkSize } : null;
      },
      async put(id, offset, chunk) {
        calls.push(offset);
        const session = sessions.get(id);
        if (!session) return { kind: "gone" };
        if (failOnce.has(offset)) {
          failOnce.delete(offset);
          return { kind: "retry", error: new Error("network") };
        }
        if (offset !== session.received) return { kind: "resume", receivedBytes: session.received };
        session.received += chunk.size;
        return { kind: "stored", receivedBytes: session.received };
      },
    },
  };
};

const file = (size) => new Blob([new Uint8Array(size)]);
const noWait = async () => {};

test("a file is sent chunk by chunk and progress reaches its size", async () => {
  const server = fakeServer();
  const store = memoryStore();
  const progress = [];

  const id = await uploadInChunks({
    file: file(10),
    transport: server.transport,
    store,
    resumeKey: "k",
    onProgress: (sent) => progress.push(sent),
    wait: noWait,
  });

  assert.deepEqual(server.calls, [0, 4, 8]);
  assert.equal(server.sessions.get(id).received, 10);
  assert.deepEqual(progress, [0, 4, 8, 10]);
});

test("a dropped connection retries the same chunk instead of starting over", async () => {
  const server = fakeServer({ failOnce: new Set([4]) });
  const waits = [];

  await uploadInChunks({
    file: file(10),
    transport: server.transport,
    store: memoryStore(),
    resumeKey: "k",
    wait: async (ms) => waits.push(ms),
  });

  assert.deepEqual(server.calls, [0, 4, 4, 8]);
  assert.deepEqual(waits, [1000]);
});

test("after a reload the saved session continues from the server's offset", async () => {
  const server = fakeServer({ existing: { id: "saved", received: 8 } });
  const store = memoryStore();
  const key = chunkedResumeKey("contract:1:Attachment", {
    name: "a.pdf",
    size: 10,
    lastModified: 5,
  });
  store.set(key, "saved");

  const id = await uploadInChunks({
    file: file(10),
    transport: server.transport,
    store,
    resumeKey: key,
    wait: noWait,
  });

  assert.equal(id, "saved");
  assert.deepEqual(server.calls, [8]);
});

test("an expired saved session starts a new one", async () => {
  const server = fakeServer();
  const store = memoryStore();
  store.set("k", "expired");

  const id = await uploadInChunks({
    file: file(4),
    transport: server.transport,
    store,
    resumeKey: "k",
    wait: noWait,
  });

  assert.notEqual(id, "expired");
  assert.equal(store.get("k"), id);
});

test("repeated failures give up with a clear error after the retries", async () => {
  const server = fakeServer();
  server.transport.put = async () => ({ kind: "retry", error: new Error("offline") });

  await assert.rejects(
    uploadInChunks({
      file: file(4),
      transport: server.transport,
      store: memoryStore(),
      resumeKey: "k",
      retries: 2,
      wait: noWait,
    }),
    (error) => error instanceof ChunkedUploadError,
  );
});

test("a cancelled upload stops before the next chunk", async () => {
  const server = fakeServer();
  const controller = new AbortController();
  const put = server.transport.put;
  server.transport.put = async (...args) => {
    const result = await put(...args);
    controller.abort();
    return result;
  };

  await assert.rejects(
    uploadInChunks({
      file: file(10),
      transport: server.transport,
      store: memoryStore(),
      resumeKey: "k",
      signal: controller.signal,
      wait: noWait,
    }),
    (error) => error.name === "AbortError",
  );
  assert.deepEqual(server.calls, [0]);
});

const { stageLargeFormFiles, CHUNKED_UPLOAD_THRESHOLD_BYTES } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("an upload form sends only its large files in chunks and names their sessions", async () => {
  const server = fakeServer({ chunkSize: CHUNKED_UPLOAD_THRESHOLD_BYTES });
  const big = new File([new Uint8Array(CHUNKED_UPLOAD_THRESHOLD_BYTES + 1)], "ban-ve.pdf");
  const small = new File([new Uint8Array(10)], "ghi-chu.pdf");
  const form = new FormData();
  form.append("category", "Drawings");
  form.append("files", small);
  form.append("files", big);
  const resumeKeys = [];
  const progress = [];

  const staged = await stageLargeFormFiles(form, {
    target: "/api/quotes/5/documents",
    transportFor: () => server.transport,
    store: memoryStore(),
    resumeKeys,
    onProgress: (sent) => progress.push(sent),
    wait: noWait,
  });

  assert.equal(staged.header, "files=s1");
  assert.equal(staged.stagedBytes, big.size);
  assert.deepEqual(
    [...staged.form.entries()].map(([field, value]) => [
      field,
      typeof value === "string" ? value : value.name,
    ]),
    [
      ["category", "Drawings"],
      ["files", "ghi-chu.pdf"],
    ],
  );
  assert.equal(resumeKeys.length, 1);
  assert.equal(progress.at(-1), big.size);
});

test("a form without large files is sent as it is", async () => {
  const form = new FormData();
  form.append("file", new File([new Uint8Array(10)], "a.pdf"));

  const staged = await stageLargeFormFiles(form, {
    target: "/api/customers/1/documents",
    transportFor: () => fakeServer().transport,
    store: memoryStore(),
    resumeKeys: [],
    wait: noWait,
  });

  assert.equal(staged, null);
});

test("the same large file twice in one form gets two sessions", async () => {
  const server = fakeServer({ chunkSize: CHUNKED_UPLOAD_THRESHOLD_BYTES });
  const big = new File([new Uint8Array(CHUNKED_UPLOAD_THRESHOLD_BYTES + 1)], "ban-ve.pdf");
  const form = new FormData();
  form.append("files", big);
  form.append("files", big);

  const staged = await stageLargeFormFiles(form, {
    target: "/api/rfqs/1/documents",
    transportFor: () => server.transport,
    store: memoryStore(),
    resumeKeys: [],
    wait: noWait,
  });

  assert.equal(staged.header, "files=s1,files=s2");
});
