/**
 * Business helpers around an Opportunity's expected close date. Extracted
 * to a util so the same overdue rule is shared between the table row,
 * the Kanban card, and the detail dialog header.
 *
 * Overdue rule (spec NIH-88 / NIH-91):
 *   ExpectedCloseDate < today  AND  stage is NOT terminal (Won / Lost)
 *
 * "Today" is compared in the browser's local time zone (start of day)
 * so a deal expected to close today is not flagged as overdue.
 */

export type TerminalCheckStage = string; // narrow via caller (OpportunityStage)

export function isOpportunityOverdue(
    expectedCloseDate: string | null | undefined,
    stage: TerminalCheckStage,
): boolean {
    if (!expectedCloseDate) return false;
    if (stage === "Won" || stage === "Lost") return false;

    const due = new Date(expectedCloseDate);
    if (Number.isNaN(due.getTime())) return false;

    const now = new Date();
    const dueDay = new Date(due.getFullYear(), due.getMonth(), due.getDate());
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    return dueDay.getTime() < today.getTime();
}
