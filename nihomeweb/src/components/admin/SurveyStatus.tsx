import { AlertTriangle, CheckCircle2, CircleDashed, HelpCircle } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { todayIsoDate } from "@/lib/displaySettings";
import { surveyProgress, type SurveyProgress } from "@/lib/surveyStatus";
import { Badge } from "@/components/ui/badge";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import type { SurveyDriveSyncStatus, SurveyStatus } from "@/services/adminApi";

const PROGRESS_STYLE: Record<SurveyProgress, string> = {
  planned: "border-border bg-muted/30 text-foreground",
  overdue: "border-warning/40 bg-warning-soft text-warning-strong",
  completed: "border-success/30 bg-success-soft text-success-strong",
};

const PROGRESS_ICON = {
  planned: CircleDashed,
  overdue: AlertTriangle,
  completed: CheckCircle2,
} satisfies Record<SurveyProgress, unknown>;

/**
 * Whether the survey took place, in words: not yet surveyed, past its day
 * without anyone recording it, or recorded as surveyed.
 */
export function SurveyStatusBadge({
  survey,
  className,
}: {
  survey: { status: SurveyStatus; surveyDate: string };
  className?: string;
}) {
  const { t } = useI18n();
  const progress = surveyProgress(survey, todayIsoDate());
  const Icon = PROGRESS_ICON[progress];
  return (
    <Badge
      variant="outline"
      className={cn("gap-1 whitespace-nowrap", PROGRESS_STYLE[progress], className)}
    >
      <Icon className="h-3 w-3" aria-hidden />
      {t(`surveys.progress.${progress}`)}
    </Badge>
  );
}

const DRIVE_STATES: SurveyDriveSyncStatus[] = ["NotSynced", "Syncing", "Synced", "Failed"];

/** Explains the Drive column: what each state means and what to do about it. */
export function DriveStatusHelp() {
  const { t } = useI18n();
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="inline-flex h-6 w-6 items-center justify-center rounded-full text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          aria-label={t("surveys.driveHelp.open")}
          onClick={(event) => event.stopPropagation()}
        >
          <HelpCircle className="h-4 w-4" aria-hidden />
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-80 text-sm normal-case tracking-normal" align="start">
        <p className="font-medium text-foreground">{t("surveys.driveHelp.title")}</p>
        <p className="mt-1 text-muted-foreground">{t("surveys.driveHelp.intro")}</p>
        <dl className="mt-3 space-y-2">
          {DRIVE_STATES.map((state) => (
            <div key={state}>
              <dt className="font-medium text-foreground">{t(`surveys.driveStatus.${state}`)}</dt>
              <dd className="text-muted-foreground">{t(`surveys.driveHelp.${state}`)}</dd>
            </div>
          ))}
        </dl>
      </PopoverContent>
    </Popover>
  );
}
