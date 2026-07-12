import { describe, expect, it } from "vitest";
import { isOpportunityOverdue } from "./opportunityDates";

const today = new Date();
const iso = (d: Date) => d.toISOString();

describe("isOpportunityOverdue", () => {
    it("returns false for null / undefined / invalid dates", () => {
        expect(isOpportunityOverdue(null, "Prospecting")).toBe(false);
        expect(isOpportunityOverdue(undefined, "Prospecting")).toBe(false);
        expect(isOpportunityOverdue("not-a-date", "Prospecting")).toBe(false);
    });

    it("returns true for a past date on a non-terminal stage", () => {
        const past = new Date(today.getTime() - 7 * 86_400_000);
        expect(isOpportunityOverdue(iso(past), "Prospecting")).toBe(true);
        expect(isOpportunityOverdue(iso(past), "Negotiation")).toBe(true);
    });

    it("returns false when the deal has already closed (Won / Lost)", () => {
        const past = new Date(today.getTime() - 30 * 86_400_000);
        expect(isOpportunityOverdue(iso(past), "Won")).toBe(false);
        expect(isOpportunityOverdue(iso(past), "Lost")).toBe(false);
    });

    it("returns false when today is the expected close date (edge case)", () => {
        // start-of-day compare in local TZ — same day is NOT overdue
        const midnightToday = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        expect(isOpportunityOverdue(iso(midnightToday), "Prospecting")).toBe(false);
    });

    it("returns false when the expected date is in the future", () => {
        const future = new Date(today.getTime() + 14 * 86_400_000);
        expect(isOpportunityOverdue(iso(future), "Proposal")).toBe(false);
    });
});
