type Translate = (key: string, params?: Record<string, string | number>) => string;

/**
 * Business errors with a stable code, and for some the values their message needs (a person,
 * a step, a role), so the reason reads in the user's language with those details. The one
 * table every error path uses.
 */
const CODED_ERRORS: Record<string, string> = {
  HSE_INDEPENDENT_REVIEW_REQUIRED: "hse.independentReviewRequired",
  IFC_RELEASED_FILE_CHANGED: "procurement.ifc.fileChanged",
  CONTRACT_SIGNED_SCAN_REQUIRED: "contracts.detail.transition.needScan",
  LEGAL_MEMBER_INVALID: "people.legalMemberInvalid",
  PUNCH_DESIGN_MEMBER_INVALID: "people.designMemberInvalid",
  WORK_PACKAGE_NOT_COMPLETABLE: "workPackages.error.notCompletable",
  QUOTE_APPROVAL_NOT_PENDING: "quotes.error.notPending",
  QUOTE_APPROVAL_NOT_YOUR_TURN: "quotes.error.notYourTurn",
  QUOTE_APPROVAL_NOT_CHOSEN: "quotes.error.notChosen",
  QUOTE_APPROVAL_ROLE_LOST: "quotes.error.roleLost",
  QUOTE_NEXT_APPROVER_UNAVAILABLE: "quotes.error.nextApproverUnavailable",
  QUOTE_WITHDRAW_APPROVER_AVAILABLE: "quotes.error.withdrawApproverAvailable",
  QUOTE_APPROVAL_STEP_NO_APPROVER: "quotes.error.stepNoApprover",
  QUOTE_APPROVAL_STEP_UNCHOSEN: "quotes.error.stepUnchosen",
  QUOTE_APPROVAL_STEP_INVALID_APPROVER: "quotes.error.stepInvalidApprover",
  QUOTE_APPROVAL_CHAIN_CHANGED: "quotes.error.chainChanged",
  QUOTE_APPROVAL_PERSON_TWICE: "quotes.error.personTwice",
  QUOTE_WORKFLOW_STEPS_BLOCKED: "workflow.error.stepsBlocked",
  QUOTE_WORKFLOW_EVERYONE_ROLE_REPEATED: "workflow.error.everyoneRoleRepeated",
};

/** The localized message for a coded business error, or null when the code is not one of them. */
export function codedErrorMessage(data: unknown, t: Translate): string | null {
  if (!data || typeof data !== "object" || !("code" in data) || typeof data.code !== "string")
    return null;
  const key = CODED_ERRORS[data.code];
  if (!key) return null;
  const raw =
    "params" in data && data.params && typeof data.params === "object"
      ? (data.params as Record<string, unknown>)
      : {};
  const params: Record<string, string | number> = {};
  for (const [name, value] of Object.entries(raw))
    if (typeof value === "string" || typeof value === "number") params[name] = value;
  // A role is named by its translation key, so it reads in the user's language.
  const roleOf = (item: Record<string, unknown>) =>
    item.roleLabelKey ? t(String(item.roleLabelKey)) : String(item.roleName ?? "");
  if (typeof raw.roleLabelKey === "string" || typeof raw.roleName === "string")
    params.role = roleOf(raw);
  // Several blocked steps, each said in full: "Step 2 (Board): …".
  if (Array.isArray(raw.items))
    params.steps = raw.items
      .filter((item): item is Record<string, unknown> => !!item && typeof item === "object")
      .map((item) =>
        t(`workflow.issue.${String(item.issue)}`, { n: Number(item.step), role: roleOf(item) }),
      )
      .join(" ");
  return t(key, params);
}

/**
 * Shared helper for extracting a human-friendly message from an error thrown
 * by an axios `adminApi.*` call. Used by every admin CRUD page so ModelState
 * validation errors and business-rule 400s show up in the toast instead of
 * the generic "Request failed with status code 400".
 *
 * Handles three response shapes:
 *  1. ASP.NET Core ValidationProblemDetails: `{ errors: { "Field": [ "msg" ] } }`
 *     → joins every field's messages so nothing is hidden from the user.
 *  2. Simple `{ message: "..." }` bodies from our service exceptions.
 *  3. Any other Error / value — falls back to `.message` then `String(err)`.
 */
export function extractApiError(err: unknown, fallback?: string, translate?: Translate): string {
  if (typeof err !== "object" || err === null) return fallback ?? String(err);

  const anyErr = err as {
    response?: { status?: number; data?: unknown };
    message?: string;
  };
  const data = anyErr.response?.data;

  if (data && typeof data === "object") {
    if (translate && "code" in data) {
      if (typeof data.code === "string" && data.code.startsWith("CM_SCHEDULE_")) {
        return translate(`cmSchedule.error.${data.code.slice("CM_SCHEDULE_".length)}`);
      }
      const primaryNotApproved = data.code === "IFC_PRIMARY_FILE_NOT_APPROVED";
      if (primaryNotApproved || data.code === "IFC_PRIMARY_FILE_UNAVAILABLE") {
        const key = primaryNotApproved
          ? "procurement.ifc.primaryNotApproved"
          : "procurement.ifc.primaryUnavailable";
        const message = translate(key);
        return "drawingCode" in data && typeof data.drawingCode === "string"
          ? `${data.drawingCode}: ${message}`
          : message;
      }
      const coded = codedErrorMessage(data, translate);
      if (coded) return coded;
    }
    if ("errors" in data && data.errors && typeof data.errors === "object") {
      // Imports list row errors ({ message, messageKey }); validation maps each
      // field to its messages. Never assume one shape: a crash here blanks the page.
      if (Array.isArray(data.errors)) {
        const rows = data.errors
          .map((item: unknown) => {
            if (typeof item === "string") return item;
            if (!item || typeof item !== "object") return "";
            const row = item as { message?: unknown; messageKey?: unknown };
            if (translate && typeof row.messageKey === "string") return translate(row.messageKey);
            return typeof row.message === "string" ? row.message : "";
          })
          .filter(Boolean);
        if (rows.length > 0) return rows.join(" · ");
      } else {
        return Object.entries(data.errors as Record<string, unknown>)
          .map(([field, messages]) => {
            const text = Array.isArray(messages) ? messages.join("; ") : String(messages);
            return `${field}: ${text}`;
          })
          .join(" · ");
      }
    }
    if ("message" in data && typeof (data as { message?: unknown }).message === "string") {
      return (data as { message: string }).message;
    }
    if ("detail" in data && typeof (data as { detail?: unknown }).detail === "string") {
      return (data as { detail: string }).detail;
    }
  }

  return anyErr.message ?? fallback ?? String(err);
}

/** Download requests return Blob bodies even when the server sends JSON errors. */
export async function extractDownloadError(
  err: unknown,
  fallback?: string,
  translate?: (key: string) => string,
): Promise<string> {
  if (typeof err !== "object" || err === null) return extractApiError(err, fallback, translate);
  const response = (err as { response?: { data?: unknown } }).response;
  if (response?.data instanceof Blob && response.data.size <= 64 * 1024) {
    try {
      const data: unknown = JSON.parse(await response.data.text());
      return extractApiError(
        { response: { data }, message: extractApiError(err, fallback) },
        fallback,
        translate,
      );
    } catch {
      // Non-JSON/provider failures retain the original safe fallback.
    }
  }
  return extractApiError(err, fallback, translate);
}

export function isConcurrencyConflict(err: unknown): boolean {
  if (typeof err !== "object" || err === null) return false;
  const response = (err as { response?: { status?: number; data?: unknown } }).response;
  if (response?.status !== 409 || typeof response.data !== "object" || response.data === null) {
    return false;
  }
  return (response.data as { code?: unknown }).code === "crm_concurrency_conflict";
}

/** Localized UI boundary: preserve safe API reasons; never expose runtime failures. */
export function getUiError(err: unknown, t: Translate, fallback?: string): string {
  const failure = err as {
    code?: string;
    name?: string;
    message?: string;
    response?: { status?: number; data?: unknown };
  } | null;
  if (failure?.code === "LOOKUP_INCOMPLETE") return t("common.lookupIncomplete");
  // The user stopped the upload from the upload tray (a chunked upload may stop between
  // requests, which rejects with an AbortError rather than an axios cancel).
  if (failure?.code === "ERR_CANCELED" || failure?.name === "AbortError")
    return t("upload.cancelled");
  // A chunked upload stopped: the session expired, or the connection kept dropping.
  if (failure?.code === "UPLOAD_SESSION_GONE") return t("upload.chunked.sessionGone");
  if (failure?.code === "UPLOAD_RETRIES_EXHAUSTED") return t("upload.chunked.retriesExhausted");
  if (!failure?.response) {
    const network =
      failure?.message === "Network Error" ||
      ["ERR_NETWORK", "ECONNABORTED", "ETIMEDOUT"].includes(failure?.code ?? "");
    return network ? t("auth.err.networkError") : (fallback ?? t("auth.err.unexpected"));
  }
  // The server closes an oversized upload with 413 and no explanation of its own.
  if (failure.response.status === 413) return t("upload.tooLargeForServer");
  if ((failure.response.status ?? 500) >= 500) return t("auth.err.unexpected");
  const data = failure.response.data;
  if (!data || typeof data !== "object") return t("auth.err.unexpected");
  const hasReason =
    ("message" in data && typeof data.message === "string") ||
    ("detail" in data && typeof data.detail === "string") ||
    ("errors" in data && typeof data.errors === "object" && data.errors !== null);
  if (!hasReason) return t("auth.err.unexpected");
  const message = extractApiError(err, t("auth.err.unexpected"), t);
  if (
    /\bat\s+\S+\([^)]*:\d+|\b(?:SqlException|System\.[A-Z]\w*Exception|TypeError|ReferenceError)\b|(?:\/app\/|[A-Z]:\\)/.test(
      message,
    )
  )
    return t("auth.err.unexpected");
  return message;
}
