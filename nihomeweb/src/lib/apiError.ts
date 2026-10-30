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
        data.code === "LEGAL_MEMBER_INVALID"
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
export async function extractDownloadError(err: unknown, fallback?: string): Promise<string> {
  if (typeof err !== "object" || err === null) return extractApiError(err, fallback);
  const response = (err as { response?: { data?: unknown } }).response;
  if (response?.data instanceof Blob && response.data.size <= 64 * 1024) {
    try {
      const data: unknown = JSON.parse(await response.data.text());
      return extractApiError(
        { response: { data }, message: extractApiError(err, fallback) },
        fallback,
      );
    } catch {
      // Non-JSON/provider failures retain the original safe fallback.
    }
  }
  return extractApiError(err, fallback);
}

export function isConcurrencyConflict(err: unknown): boolean {
  if (typeof err !== "object" || err === null) return false;
  const response = (err as { response?: { status?: number; data?: unknown } }).response;
  if (response?.status !== 409 || typeof response.data !== "object" || response.data === null) {
    return false;
  }
  return (response.data as { code?: unknown }).code === "crm_concurrency_conflict";
}
