import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

// Exercise the real dependency-free helper without requiring a browser or a new runner.
const source = await readFile(new URL("../src/lib/apiError.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { extractDownloadError, extractApiError } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

const failure = (data) => ({ response: { status: 409, data }, message: "HTTP 409" });

test("missing signed scan uses the localized Documents guidance", () => {
  assert.equal(
    extractApiError(
      failure({ code: "CONTRACT_SIGNED_SCAN_REQUIRED", message: "Server reason" }),
      undefined,
      (key) =>
        key === "contracts.detail.transition.needScan" ? "Upload the scan in Documents." : key,
    ),
    "Upload the scan in Documents.",
  );
});

test("member eligibility codes use localized guidance and preserve unknown server reasons", () => {
  const translate = (key) =>
    ({
      "people.legalMemberInvalid": "Select a member of the shared project team.",
      "people.designMemberInvalid": "Select a designer in this flow.",
    })[key];
  assert.equal(
    extractApiError(
      failure({ code: "LEGAL_MEMBER_INVALID", field: "assigneeUserId", message: "Server reason" }),
      undefined,
      translate,
    ),
    "Select a member of the shared project team.",
  );
  assert.equal(
    extractApiError(
      failure({
        code: "PUNCH_DESIGN_MEMBER_INVALID",
        field: "responsibleDesignUserId",
        message: "Server reason",
      }),
      undefined,
      translate,
    ),
    "Select a designer in this flow.",
  );
  assert.equal(
    extractApiError(
      failure({ code: "OTHER", message: "Keep the specific server reason" }),
      undefined,
      translate,
    ),
    "Keep the specific server reason",
  );
  assert.equal(
    extractApiError(failure({ code: "LEGAL_MEMBER_INVALID", message: "Server reason" })),
    "Server reason",
  );
});

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

test("UI failures localize transport errors and hide unexpected implementation details", async () => {
  const { getUiError } = await import(
    "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
  );
  const translate = (key) =>
    ({
      "auth.err.networkError": "Check your connection and retry.",
      "auth.err.unexpected": "The operation did not complete. Try again later.",
    })[key] ?? key;
  assert.equal(
    getUiError(new Error("Network Error"), translate),
    "Check your connection and retry.",
  );
  assert.equal(
    getUiError(new Error("TypeError: hidden path /app/secrets.cs"), translate),
    "The operation did not complete. Try again later.",
  );
  assert.equal(
    getUiError(
      { response: { status: 500, data: { detail: "SqlException at /app/Data.cs" } } },
      translate,
    ),
    "The operation did not complete. Try again later.",
  );
  assert.equal(
    getUiError(failure({ message: "Refresh the latest record before retrying." }), translate),
    "Refresh the latest record before retrying.",
  );
  assert.equal(
    getUiError(failure({ errors: { Phone: ["Enter a valid phone number."] } }), translate),
    "Phone: Enter a valid phone number.",
  );
});
