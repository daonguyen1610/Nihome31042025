import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { formatDateTime } from "@/lib/displaySettings";
import { adminApi, type DesignApprovalInboxResponse } from "@/services/adminApi";

const submitted = (iso: string | null | undefined, lang: string) =>
  iso ? formatDateTime(iso, lang) : "—";

/** "Chờ tôi duyệt": schedule tasks and internal reviews waiting on the signed-in user. */
export default function DesignApprovals() {
  const { t, lang } = useI18n();
  const uiError = useUiError();
  const [data, setData] = useState<DesignApprovalInboxResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      setData((await adminApi.getDesignApprovalInbox()).data);
    } catch (reason) {
      setError(uiError(reason));
    }
  }, [uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <AdminLayout>
      <div className="mx-auto w-full max-w-5xl space-y-4 p-3 md:p-4">
        <header>
          <h1 className="text-xl font-bold md:text-2xl">{t("designApprovals.title")}</h1>
          <p className="text-sm text-muted-foreground">{t("designApprovals.subtitle")}</p>
        </header>

        {error ? (
          <PageError message={error} onRetry={() => void load()} />
        ) : !data ? (
          <PageLoading />
        ) : data.tasks.length === 0 && data.reviews.length === 0 ? (
          <PageEmpty message={t("designApprovals.empty")} />
        ) : (
          <>
            <section aria-labelledby="design-approvals-tasks" className="space-y-2">
              <h2 id="design-approvals-tasks" className="text-base font-semibold">
                {t("designApprovals.tasks.title", { count: data.tasks.length })}
              </h2>
              {data.tasks.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("designApprovals.tasks.none")}</p>
              ) : (
                <ul className="divide-y rounded-lg border bg-card">
                  {data.tasks.map((task) => (
                    <li key={task.taskId}>
                      <Link
                        to={`/admin/design-projects/${task.designProjectId}?tab=schedule`}
                        className="block p-3 hover:bg-muted/50"
                      >
                        <span className="block font-medium">
                          {task.code} · {task.name}
                        </span>
                        <span className="block text-sm text-muted-foreground">
                          {t("designApprovals.tasks.meta", {
                            project: task.designProjectName,
                            assignee: task.assigneeName ?? "—",
                            submittedAt: submitted(task.submittedAt, lang),
                          })}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section aria-labelledby="design-approvals-reviews" className="space-y-2">
              <h2 id="design-approvals-reviews" className="text-base font-semibold">
                {t("designApprovals.reviews.title", { count: data.reviews.length })}
              </h2>
              {data.reviews.length === 0 ? (
                <p className="text-sm text-muted-foreground">{t("designApprovals.reviews.none")}</p>
              ) : (
                <ul className="divide-y rounded-lg border bg-card">
                  {data.reviews.map((review) => (
                    <li key={review.reviewId}>
                      <Link
                        to={`/admin/design-reviews/${review.designProjectId}?reviewId=${review.reviewId}`}
                        className="flex flex-wrap items-start justify-between gap-2 p-3 hover:bg-muted/50"
                      >
                        <span className="min-w-0">
                          <span className="block font-medium">{review.title}</span>
                          <span className="block text-sm text-muted-foreground">
                            {t("designApprovals.reviews.meta", {
                              project: review.designProjectName,
                              stage: t(`designProjects.stage.${review.stage}`),
                              submittedAt: submitted(review.submittedAt, lang),
                            })}
                          </span>
                        </span>
                        <Badge variant="outline" className="whitespace-nowrap">
                          {t(
                            review.hasMyComment
                              ? "designApprovals.reviews.commented"
                              : "designApprovals.reviews.awaitingComment",
                          )}
                        </Badge>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>
    </AdminLayout>
  );
}
