import { useCallback, useEffect, useState } from "react";
import { ArrowRight } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useUiError } from "@/hooks/useUiError";
import { formatNumber } from "@/lib/displaySettings";
import { InlineWarning } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { adminApi, type DesignProjectResponse, type DesignProjectStage } from "@/services/adminApi";

/** Tab that holds the work which moves each stage forward. */
const NEXT_STEP_TAB: Record<DesignProjectStage, string> = {
  Concept: "concept",
  BasicDesign: "basic",
  ShopDrawing: "ifc",
  Completed: "ifc",
};

type Counts = {
  tasksPending?: number;
  tasksOverdue?: number;
  reviewsInternal?: number;
  reviewsClient?: number;
};

const isForbidden = (reason: unknown) =>
  (reason as { response?: { status?: number } })?.response?.status === 403;

/** Overview card: what moves the dossier forward and what is waiting on someone. */
export function DesignNextStepPanel({
  project,
  onOpenTab,
}: {
  project: DesignProjectResponse;
  onOpenTab: (tab: string) => void;
}) {
  const { t } = useI18n();
  const uiError = useUiError();
  const [counts, setCounts] = useState<Counts>({});
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    const scheduleId = project.operationalProjectId;
    const schedule = (filter: { pendingApprovalOnly?: boolean; overdueOnly?: boolean }) =>
      scheduleId
        ? adminApi
            .getDesignSchedule(scheduleId, { designProjectId: project.id, pageSize: 1, ...filter })
            .then(({ data }) => data.tasks.totalCount)
        : Promise.resolve(undefined);
    const [pending, overdue, reviews] = await Promise.allSettled([
      schedule({ pendingApprovalOnly: true }),
      schedule({ overdueOnly: true }),
      adminApi.listDesignReviews(project.id).then(({ data }) => data),
    ]);
    const failure = [pending, overdue, reviews].find(
      (result): result is PromiseRejectedResult =>
        result.status === "rejected" && !isForbidden(result.reason),
    );
    if (failure) setError(uiError(failure.reason));
    const reviewList = reviews.status === "fulfilled" ? reviews.value : null;
    setCounts({
      tasksPending: pending.status === "fulfilled" ? pending.value : undefined,
      tasksOverdue: overdue.status === "fulfilled" ? overdue.value : undefined,
      reviewsInternal: reviewList?.filter((review) => review.status === "InternalReview").length,
      reviewsClient: reviewList?.filter((review) => review.status === "SubmittedToClient").length,
    });
  }, [project.id, project.operationalProjectId, uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  const nextTab = NEXT_STEP_TAB[project.currentStage];
  const waiting = (
    [
      ["tasksPending", "schedule"],
      ["tasksOverdue", "schedule"],
      ["reviewsInternal", "reviews"],
      ["reviewsClient", "reviews"],
    ] as const
  ).filter(([key]) => counts[key] !== undefined);

  return (
    <section
      className="rounded-lg border border-border bg-card p-4 shadow-sm"
      aria-labelledby="design-next-step-title"
    >
      <h2 id="design-next-step-title" className="text-base font-semibold">
        {t("designProjects.overview.nextStep.title")}
      </h2>
      <p className="mt-1 text-sm text-foreground">
        {t(`designProjects.overview.nextStep.${project.currentStage}`)}
      </p>
      {project.currentStage !== "Completed" && (
        <Button variant="outline" size="sm" className="mt-3" onClick={() => onOpenTab(nextTab)}>
          {t(`designProjects.detail.tab.${nextTab}`)}
          <ArrowRight className="ml-1 h-4 w-4" aria-hidden />
        </Button>
      )}

      {error && (
        <div className="mt-4">
          <InlineWarning message={error} onRetry={() => void load()} />
        </div>
      )}
      {waiting.length > 0 && (
        <dl className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-4">
          {waiting.map(([key, tab]) => (
            <div key={key}>
              <dt className="text-xs text-muted-foreground">
                {t(`designProjects.overview.count.${key}`)}
              </dt>
              <dd>
                <button
                  type="button"
                  className={cn(
                    "text-2xl font-semibold tabular-nums underline-offset-4 hover:underline",
                    counts[key] ? "text-foreground" : "text-muted-foreground",
                  )}
                  onClick={() => onOpenTab(tab)}
                  aria-label={`${t(`designProjects.overview.count.${key}`)}: ${formatNumber(counts[key]!)}`}
                >
                  {formatNumber(counts[key]!)}
                </button>
              </dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  );
}
