import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

// Exercise the real dependency-free helper without requiring a browser or a new runner.
const source = await readFile(new URL("../src/lib/apiError.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { extractDownloadError } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const failure = (data) => ({ response: { status: 409, data }, message: "HTTP 409" });

test("download conflict preserves the server's recovery message from a JSON Blob", async () => {
  const message = "Nội dung tệp không khớp bản đã trình. Liên hệ Chủ nhiệm thiết kế.";
  assert.equal(
    await extractDownloadError(failure(new Blob([JSON.stringify({ message })]))),
    message,
  );
});

test("download validation and problem details use the shared error rules", async () => {
  assert.equal(
    await extractDownloadError(
      failure(new Blob([JSON.stringify({ errors: { File: ["Too large"] } })])),
    ),
    "File: Too large",
  );
  assert.equal(
    await extractDownloadError(failure(new Blob([JSON.stringify({ detail: "Retry later" })]))),
    "Retry later",
  );
});

for (const [name, data] of [
  ["non-JSON", new Blob(["<html>Provider unavailable</html>"])],
  ["empty", new Blob([])],
  ["unrecognized JSON", new Blob(["{}", " "])],
  ["oversized", new Blob([JSON.stringify({ message: "x".repeat(65 * 1024) })])],
]) {
  test(`download ${name} body retains a safe fallback`, async () => {
    assert.equal(await extractDownloadError(failure(data)), "HTTP 409");
  });
}

test("ordinary API errors and network failures keep existing extraction", async () => {
  assert.equal(
    await extractDownloadError(failure({ message: "Permission denied" })),
    "Permission denied",
  );
  assert.equal(await extractDownloadError(new Error("Offline")), "Offline");
  assert.equal(await extractDownloadError(null, "Please retry"), "Please retry");
});
