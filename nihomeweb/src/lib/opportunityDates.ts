/**
 * Business helpers around an Opportunity's expected close date. Extracted
 * to a util so the same overdue rule is shared between the table row,
 * the Kanban card, and the detail dialog header.
 *
 * Overdue rule (spec NIH-88 / NIH-91):
 *   ExpectedCloseDate < today  AND  stage is NOT terminal (Won / Lost)
 *
 * "Today" is compared in the configured display time zone so a deal
 * expected to close today is not flagged as overdue.
 */
import { todayIsoDate } from "@/lib/displaySettings";

export type TerminalCheckStage = string; // narrow via caller (OpportunityStage)

export function isOpportunityOverdue(
  expectedCloseDate: string | null | undefined,
  stage: TerminalCheckStage,
): boolean {
  if (!expectedCloseDate) return false;
  if (stage === "Won" || stage === "Lost") return false;

  const dueDay = expectedCloseDate.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(dueDay)) return false;
  return dueDay < todayIsoDate();
}
