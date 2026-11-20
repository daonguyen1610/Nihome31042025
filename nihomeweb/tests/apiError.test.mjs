import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

// Exercise the real dependency-free helper without requiring a browser or a new runner.
const source = await readFile(new URL("../src/lib/apiError.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { extractDownloadError, extractApiError, getUiError } = await import(
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

test("work package close rejections use localized guidance in every language", async () => {
  const fs = await import("node:fs");
  const seed = JSON.parse(
    fs.readFileSync(
      new URL("../../nihomebackend/Data/Seeds/i18n/work-packages.json", import.meta.url),
      "utf8",
    ),
  );
  const entry = seed.find((item) => item.key === "workPackages.error.notCompletable");
  for (const lang of ["vi", "en", "zh", "ja"]) {
    const expected = entry[lang];
    assert.ok(expected);
    assert.equal(
      extractApiError(
        failure({ code: "WORK_PACKAGE_NOT_COMPLETABLE", message: "server fallback" }),
        undefined,
        (key) => (key === entry.key ? expected : key),
      ),
      expected,
    );
  }
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

// Blob errors must use the same localized guidance as JSON API failures.
test("released IFC mismatch localizes download errors in every supported language", async () => {
  const fs = await import("node:fs");
  const seed = JSON.parse(
    fs.readFileSync(
      new URL("../../nihomebackend/Data/Seeds/i18n/procurement-ifc.json", import.meta.url),
      "utf8",
    ),
  );
  const entry = seed.find((item) => item.key === "procurement.ifc.fileChanged");
  for (const lang of ["vi", "en", "zh", "ja"]) {
    const expected = entry[lang];
    assert.ok(expected);
    assert.equal(
      await extractDownloadError(
        failure(
          new Blob([
            JSON.stringify({ code: "IFC_RELEASED_FILE_CHANGED", message: "server fallback" }),
          ]),
        ),
        undefined,
        (key) => (key === entry.key ? expected : key),
      ),
      expected,
    );
  }
});

test("IFC primary-file release errors retain drawing context in all languages", async () => {
  const fs = await import("node:fs");
  const seed = JSON.parse(
    fs.readFileSync(
      new URL("../../nihomebackend/Data/Seeds/i18n/procurement-ifc.json", import.meta.url),
      "utf8",
    ),
  );
  for (const [code, key] of [
    ["IFC_PRIMARY_FILE_NOT_APPROVED", "procurement.ifc.primaryNotApproved"],
    ["IFC_PRIMARY_FILE_UNAVAILABLE", "procurement.ifc.primaryUnavailable"],
  ]) {
    for (const lang of ["vi", "en", "zh", "ja"]) {
      const expected = seed.find((entry) => entry.key === key)[lang];
      assert.equal(
        extractApiError(failure({ code, drawingCode: "KC-SD-001" }), undefined, (actual) =>
          actual === key ? expected : actual,
        ),
        `KC-SD-001: ${expected}`,
      );
    }
  }
});

test("an upload refused by the server for its size is explained, not reported as unexpected", () => {
  const t = (key) => key;
  assert.equal(
    getUiError({ response: { status: 413, data: "" }, message: "HTTP 413" }, t),
    "upload.tooLargeForServer",
  );
});

test("a stopped chunked upload explains why and what to do", () => {
  const t = (key) => key;
  assert.equal(
    getUiError({ code: "UPLOAD_SESSION_GONE", message: "expired" }, t),
    "upload.chunked.sessionGone",
  );
  assert.equal(
    getUiError({ code: "UPLOAD_RETRIES_EXHAUSTED", message: "offline" }, t),
    "upload.chunked.retriesExhausted",
  );
});

test("an upload the user cancelled says so instead of reporting an error", () => {
  const t = (key) => key;
  assert.equal(getUiError({ code: "ERR_CANCELED", message: "canceled" }, t), "upload.cancelled");
  // A chunked upload cancelled between chunks rejects with an AbortError.
  assert.equal(
    getUiError(new DOMException("The upload was cancelled.", "AbortError"), t),
    "upload.cancelled",
  );
});

test("an import that lists row errors is explained instead of crashing the page", () => {
  // The material-rate import answers 400 with an array of row errors, not the
  // ASP.NET field → messages map; the upload tray renders it on every page.
  const failure = {
    response: {
      status: 400,
      data: {
        importedCount: 0,
        errors: [
          {
            row: 5,
            column: null,
            message: "Tệp phải có ít nhất một dòng dữ liệu.",
            messageKey: "materialRates.csvError.dataRequired",
          },
          { row: 6, message: "Đơn giá phải lớn hơn 0." },
        ],
      },
    },
  };
  const t = (key) =>
    key === "materialRates.csvError.dataRequired"
      ? "The file must contain at least one data row."
      : key;
  assert.equal(
    extractApiError(failure, undefined, t),
    "The file must contain at least one data row. · Đơn giá phải lớn hơn 0.",
  );
  assert.equal(
    getUiError(failure, t),
    "The file must contain at least one data row. · Đơn giá phải lớn hơn 0.",
  );
});

test("a field error given as a single string is shown as it is", () => {
  const failure = { response: { status: 400, data: { errors: { Name: "Tên là bắt buộc." } } } };
  assert.equal(extractApiError(failure), "Name: Tên là bắt buộc.");
});

test("coded approval errors read in the user's language with the person, step and role", () => {
  const messages = {
    "quotes.error.notYourTurn": "Not your turn: waiting for {name} at step {current} of {total}.",
    "workflow.error.stepsBlocked": "Not saved: nobody can approve these steps. {steps}",
    "workflow.issue.RoleCannotApprove": "Step {n} ({role}): cannot view and approve quotes.",
    "workflow.issue.RoleInactive": "Step {n} ({role}): no longer in use.",
    "rbac.role.BGD.label": "Board of Directors",
  };
  const translate = (key, params = {}) =>
    (messages[key] ?? key).replace(/\{(\w+)\}/g, (_, name) => String(params[name] ?? ""));

  assert.equal(
    getUiError(
      failure({
        code: "QUOTE_APPROVAL_NOT_YOUR_TURN",
        message: "Chưa tới lượt bạn duyệt.",
        params: { name: "Nguyễn Thị Thu Trang", current: 1, total: 2 },
      }),
      translate,
    ),
    "Not your turn: waiting for Nguyễn Thị Thu Trang at step 1 of 2.",
  );
  // Every blocked step is named, its role from its label key (or its name without one).
  assert.equal(
    getUiError(
      failure({
        code: "QUOTE_WORKFLOW_STEPS_BLOCKED",
        message: "Chưa lưu được luồng duyệt báo giá.",
        params: {
          items: [
            {
              step: 2,
              issue: "RoleCannotApprove",
              roleLabelKey: "rbac.role.BGD.label",
              roleName: "BGD",
            },
            { step: 3, issue: "RoleInactive", roleLabelKey: "", roleName: "Site lead" },
          ],
        },
      }),
      translate,
    ),
    "Not saved: nobody can approve these steps. Step 2 (Board of Directors): cannot view and approve quotes. Step 3 (Site lead): no longer in use.",
  );
  // An unknown code keeps the server's own reason.
  assert.equal(
    getUiError(failure({ code: "SOMETHING_ELSE", message: "Lý do từ máy chủ." }), translate),
    "Lý do từ máy chủ.",
  );
});
