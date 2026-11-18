/**
 * How a survey reads to staff: recorded as surveyed, still planned, or past its
 * day without anyone recording it. Free of imports so it can be unit-tested.
 */
export type SurveyProgress = "planned" | "overdue" | "completed";

/** `today` is the calendar day (YYYY-MM-DD) in the display time zone. */
export function surveyProgress(
  survey: { status: "Planned" | "Completed"; surveyDate: string },
  today: string,
): SurveyProgress {
  if (survey.status === "Completed") return "completed";
  // Survey dates are calendar days stored at midnight UTC.
  return survey.surveyDate.slice(0, 10) < today ? "overdue" : "planned";
}

/** "08:30:00" → "08:30"; null when no time was agreed. */
export function surveyTimeLabel(time?: string | null): string | null {
  return time ? time.slice(0, 5) : null;
}
