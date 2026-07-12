import { describe, expect, it } from "vitest";
import { extractApiError } from "./apiError";

describe("extractApiError", () => {
    it("returns String(err) for non-object inputs", () => {
        expect(extractApiError("boom")).toBe("boom");
        expect(extractApiError(42)).toBe("42");
        expect(extractApiError(null)).toBe("null");
    });

    it("joins ModelState errors across every field with ' · '", () => {
        const err = {
            response: {
                data: {
                    errors: {
                        Name: ["Required"],
                        WinProbability: ["Must be 0-100"],
                    },
                },
            },
        };
        const msg = extractApiError(err);
        expect(msg).toContain("Name: Required");
        expect(msg).toContain("WinProbability: Must be 0-100");
        expect(msg).toContain(" · ");
    });

    it("joins multiple messages within one field with '; '", () => {
        const err = {
            response: {
                data: {
                    errors: {
                        Name: ["Required", "Max length exceeded"],
                    },
                },
            },
        };
        expect(extractApiError(err)).toBe("Name: Required; Max length exceeded");
    });

    it("prefers server 'message' when no ModelState errors are present", () => {
        const err = {
            response: { data: { message: "Không có quyền tạo cơ hội." } },
            message: "Request failed with status code 400",
        };
        expect(extractApiError(err)).toBe("Không có quyền tạo cơ hội.");
    });

    it("falls back to Error.message when the payload has no known shape", () => {
        const err = new Error("Network Error");
        expect(extractApiError(err)).toBe("Network Error");
    });

    it("falls back to String(err) when nothing usable is present", () => {
        expect(extractApiError({})).toBe("[object Object]");
    });

    it("handles a response body that is not an object without throwing", () => {
        const err = { response: { data: "raw text" }, message: "req failed" };
        expect(extractApiError(err)).toBe("req failed");
    });
});
