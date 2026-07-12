import { describe, expect, it } from "vitest";
import { formatVnd, formatVndWithSymbol, parseVnd } from "./numberFormat";

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

describe("parseVnd", () => {
    it("returns 0 for empty input", () => {
        expect(parseVnd("")).toBe(0);
        expect(parseVnd("   ")).toBe(0);
    });

    it("strips vi-VN dot separators and returns the underlying integer", () => {
        expect(parseVnd("1.234.567")).toBe(1_234_567);
        expect(parseVnd("2.000.000.000")).toBe(2_000_000_000);
    });

    it("strips comma separators and any trailing currency symbol", () => {
        expect(parseVnd("1,234,567")).toBe(1_234_567);
        expect(parseVnd("500.000 ₫")).toBe(500_000);
    });

    it("returns the raw integer when there are no separators", () => {
        expect(parseVnd("42")).toBe(42);
    });

    it("returns 0 when the input has no digits", () => {
        expect(parseVnd("abc")).toBe(0);
        expect(parseVnd("₫")).toBe(0);
    });
});
