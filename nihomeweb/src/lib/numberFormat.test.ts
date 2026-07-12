import { describe, expect, it } from "vitest";
import { formatVnd, formatVndWithSymbol } from "./numberFormat";

describe("formatVnd", () => {
    it("groups thousands with '.'", () => {
        // Node's vi-VN locale should produce '1.234.567' for regular numbers
        expect(formatVnd(1_234_567)).toBe("1.234.567");
        expect(formatVnd(0)).toBe("0");
    });

    it("returns em-dash for null / undefined / NaN so cells never render 'NaN'", () => {
        expect(formatVnd(null)).toBe("—");
        expect(formatVnd(undefined)).toBe("—");
        expect(formatVnd(Number.NaN)).toBe("—");
    });

    it("appends the ₫ symbol variant when requested", () => {
        expect(formatVndWithSymbol(1_000)).toBe("1.000 ₫");
        expect(formatVndWithSymbol(null)).toBe("—");
    });
});
