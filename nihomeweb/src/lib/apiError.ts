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
export function extractApiError(err: unknown): string {
    if (typeof err !== "object" || err === null) return String(err);

    const anyErr = err as {
        response?: { data?: unknown };
        message?: string;
    };
    const data = anyErr.response?.data;

    if (data && typeof data === "object") {
        if ("errors" in data && data.errors && typeof data.errors === "object") {
            return Object.entries(data.errors as Record<string, string[]>)
                .map(([field, messages]) => `${field}: ${messages.join("; ")}`)
                .join(" · ");
        }
        if ("message" in data && typeof (data as { message?: unknown }).message === "string") {
            return (data as { message: string }).message;
        }
    }

    return anyErr.message ?? String(err);
}
