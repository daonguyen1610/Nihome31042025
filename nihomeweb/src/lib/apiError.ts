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
export function extractApiError(
  err: unknown,
  fallback?: string,
  translate?: (key: string) => string,
): string {
  if (typeof err !== "object" || err === null) return fallback ?? String(err);

  const anyErr = err as {
    response?: { status?: number; data?: unknown };
    message?: string;
  };
  const data = anyErr.response?.data;

  if (data && typeof data === "object") {
    if (translate && "code" in data) {
      const key =
        data.code === "IFC_RELEASED_FILE_CHANGED"
          ? "procurement.ifc.fileChanged"
          : data.code === "CONTRACT_SIGNED_SCAN_REQUIRED"
            ? "contracts.detail.transition.needScan"
            : data.code === "LEGAL_MEMBER_INVALID"
              ? "people.legalMemberInvalid"
              : data.code === "PUNCH_DESIGN_MEMBER_INVALID"
                ? "people.designMemberInvalid"
                : null;
      if (key) return translate(key);
    }
    if ("errors" in data && data.errors && typeof data.errors === "object") {
      return Object.entries(data.errors as Record<string, string[]>)
        .map(([field, messages]) => `${field}: ${messages.join("; ")}`)
        .join(" · ");
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
export function getUiError(err: unknown, t: (key: string) => string, fallback?: string): string {
  const failure = err as {
    code?: string;
    message?: string;
    response?: { status?: number; data?: unknown };
  } | null;
  if (failure?.code === "LOOKUP_INCOMPLETE") return t("common.lookupIncomplete");
  if (!failure?.response) {
    const network =
      failure?.message === "Network Error" ||
      ["ERR_NETWORK", "ECONNABORTED", "ETIMEDOUT"].includes(failure?.code ?? "");
    return network ? t("auth.err.networkError") : (fallback ?? t("auth.err.unexpected"));
  }
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
