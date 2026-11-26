import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { RefreshCcw } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { InlineWarning, PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ListPagination } from "@/components/admin/ListPagination";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { FieldScope } from "@/components/ui/field-scope";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useI18n } from "@/lib/i18n";
import { useUiError } from "@/hooks/useUiError";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { cn } from "@/lib/utils";
import {
  formatDate,
  formatDateTime,
  toDateTimeLocalValue,
  todayIsoDate,
} from "@/lib/displaySettings";
import {
  adminApi,
  type DesignApprovalInboxSummary,
  type DesignReviewStage,
} from "@/services/adminApi";
import { ApprovalFilters } from "./design/approvals/ApprovalFilters";
import {
  EMPTY_BASE_FILTERS,
  hasBaseFilters,
  type BaseApprovalFilters,
} from "./design/approvals/filters";
import { useApprovalList } from "./design/approvals/useApprovalList";

const PAGE_SIZE = 20;
/** Waiting this many days or more is shown as needing attention. */
const LONG_WAIT_DAYS = 3;
const REVIEW_STAGES: DesignReviewStage[] = ["Concept", "BasicDesign", "ShopDrawing"];
type Tab = "tasks" | "reviews";
type CommentFilter = "all" | "pending" | "done";
type ReviewFilters = BaseApprovalFilters & {
  stage: DesignReviewStage | null;
  comment: CommentFilter;
};

const EMPTY_REVIEW_FILTERS: ReviewFilters = { ...EMPTY_BASE_FILTERS, stage: null, comment: "all" };

/** Whole calendar days (display time zone) since the item was submitted. */
function daysWaiting(submittedAt: string): number {
  const toUtc = (iso: string) =>
    Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10));
  const submittedDay = toDateTimeLocalValue(submittedAt).slice(0, 10);
  return Math.max(0, Math.round((toUtc(todayIsoDate()) - toUtc(submittedDay)) / 86_400_000));
}

/** "Chờ tôi duyệt": schedule tasks and internal reviews waiting on the signed-in user. */
export default function DesignApprovals() {
  const { t } = useI18n();
  const uiError = useUiError();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab: Tab = searchParams.get("tab") === "reviews" ? "reviews" : "tasks";
  const [summary, setSummary] = useState<DesignApprovalInboxSummary | null>(null);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const loadSummary = useCallback(async () => {
    setSummaryError(null);
    try {
      setSummary((await adminApi.getDesignApprovalSummary()).data);
    } catch (reason) {
      setSummaryError(uiError(reason));
    }
  }, [uiError]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary, refreshKey]);

  const count = (value: number | undefined) => (value === undefined ? "…" : String(value));

  return (
    <AdminLayout>
      <div className="space-y-5">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <h1 className="text-2xl font-semibold">{t("designApprovals.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("designApprovals.subtitle")}</p>
          </div>
          <Button variant="outline" onClick={() => setRefreshKey((key) => key + 1)}>
            <RefreshCcw className="mr-1 h-4 w-4" aria-hidden />
            {t("common.refresh")}
          </Button>
        </header>

        {summaryError && (
          <InlineWarning message={summaryError} onRetry={() => void loadSummary()} />
        )}

        <Tabs
          value={tab}
          onValueChange={(value) =>
            setSearchParams(
              (current) => {
                const next = new URLSearchParams(current);
                next.set("tab", value);
                return next;
              },
              { replace: true },
            )
          }
        >
          <TabsList>
            <TabsTrigger value="tasks">
              {t("designApprovals.tab.tasks", { count: count(summary?.taskCount) })}
            </TabsTrigger>
            <TabsTrigger value="reviews">
              {t("designApprovals.tab.reviews", { count: count(summary?.reviewCount) })}
            </TabsTrigger>
          </TabsList>
          <TabsContent value="tasks" className="mt-4">
            <TaskInbox dossiers={summary?.dossiers ?? []} refreshKey={refreshKey} />
          </TabsContent>
          <TabsContent value="reviews" className="mt-4">
            <ReviewInbox dossiers={summary?.dossiers ?? []} refreshKey={refreshKey} />
          </TabsContent>
        </Tabs>
      </div>
    </AdminLayout>
  );
}

type InboxProps = { dossiers: DesignApprovalInboxSummary["dossiers"]; refreshKey: number };

function TaskInbox({ dossiers, refreshKey }: InboxProps) {
  const { t, lang } = useI18n();
  const [filters, setFilters] = useState<BaseApprovalFilters>(EMPTY_BASE_FILTERS);
  const [page, setPage] = useState(1);
  const search = useDebouncedValue(filters.search.trim());
  const load = useCallback(
    () =>
      adminApi.listDesignApprovalTasks({
        search: search || undefined,
        designProjectId: filters.designProjectId ?? undefined,
        newestFirst: filters.newestFirst || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    [search, filters.designProjectId, filters.newestFirst, page],
  );
  const list = useApprovalList(load, refreshKey);
  const filtered = hasBaseFilters(filters);
  const update = (next: Partial<BaseApprovalFilters>) => {
    setFilters((current) => ({ ...current, ...next }));
    setPage(1);
  };
  const reset = () => update(EMPTY_BASE_FILTERS);

  return (
    <div className="space-y-4">
      <ApprovalFilters
        idPrefix="approval-tasks"
        searchPlaceholder={t("designApprovals.tasks.searchPlaceholder")}
        filters={filters}
        onChange={update}
        dossiers={dossiers}
        hasActiveFilters={filtered}
        onReset={reset}
      />
      <InboxResults
        list={list}
        filtered={filtered}
        emptyMessage={t("designApprovals.tasks.empty")}
        page={page}
        onPageChange={setPage}
      >
        {list.data?.items.map((task) => (
          <InboxRow
            key={task.taskId}
            title={`${task.code} · ${task.name}`}
            dossier={`${task.designProjectCode} · ${task.designProjectName}`}
            details={[
              [t("designApprovals.field.phase"), t(`designProjects.stage.${task.phaseCode}`)],
              [t("designApprovals.field.assignee"), task.assigneeName ?? "—"],
              [
                t("designApprovals.field.submittedAt"),
                task.submittedAt ? formatDateTime(task.submittedAt, lang) : "—",
              ],
              [t("designApprovals.field.plannedEnd"), formatDate(task.plannedEnd, lang)],
            ]}
            submittedAt={task.submittedAt}
            action={
              <Button asChild variant="outline" size="sm">
                <Link to={`/admin/design-projects/${task.designProjectId}?tab=schedule`}>
                  {t("designApprovals.action.approve")}
                </Link>
              </Button>
            }
          />
        ))}
      </InboxResults>
    </div>
  );
}

function ReviewInbox({ dossiers, refreshKey }: InboxProps) {
  const { t, lang } = useI18n();
  const [filters, setFilters] = useState<ReviewFilters>(EMPTY_REVIEW_FILTERS);
  const [page, setPage] = useState(1);
  const search = useDebouncedValue(filters.search.trim());
  const load = useCallback(
    () =>
      adminApi.listDesignApprovalReviews({
        search: search || undefined,
        designProjectId: filters.designProjectId ?? undefined,
        stage: filters.stage ?? undefined,
        commented: filters.comment === "all" ? undefined : filters.comment === "done",
        newestFirst: filters.newestFirst || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    [search, filters.designProjectId, filters.stage, filters.comment, filters.newestFirst, page],
  );
  const list = useApprovalList(load, refreshKey);
  const filtered = hasBaseFilters(filters) || filters.stage != null || filters.comment !== "all";
  const update = (next: Partial<ReviewFilters>) => {
    setFilters((current) => ({ ...current, ...next }));
    setPage(1);
  };
  const reset = () => update(EMPTY_REVIEW_FILTERS);

  return (
    <div className="space-y-4">
      <ApprovalFilters
        idPrefix="approval-reviews"
        searchPlaceholder={t("designApprovals.reviews.searchPlaceholder")}
        filters={filters}
        onChange={update}
        dossiers={dossiers}
        hasActiveFilters={filtered}
        onReset={reset}
      >
        <FieldScope>
          <Label htmlFor="approval-reviews-stage">{t("designApprovals.filter.stage")}</Label>
          <Select
            value={filters.stage ?? "__all__"}
            onValueChange={(value) =>
              update({ stage: value === "__all__" ? null : (value as DesignReviewStage) })
            }
          >
            <SelectTrigger id="approval-reviews-stage" className="mt-1 h-9">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">{t("designApprovals.filter.allStages")}</SelectItem>
              {REVIEW_STAGES.map((stage) => (
                <SelectItem key={stage} value={stage}>
                  {t(`designProjects.stage.${stage}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FieldScope>
        <FieldScope>
          <Label htmlFor="approval-reviews-comment">{t("designApprovals.filter.comment")}</Label>
          <Select
            value={filters.comment}
            onValueChange={(value) => update({ comment: value as CommentFilter })}
          >
            <SelectTrigger id="approval-reviews-comment" className="mt-1 h-9">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("designApprovals.comment.all")}</SelectItem>
              <SelectItem value="pending">{t("designApprovals.comment.pending")}</SelectItem>
              <SelectItem value="done">{t("designApprovals.comment.done")}</SelectItem>
            </SelectContent>
          </Select>
        </FieldScope>
      </ApprovalFilters>
      <InboxResults
        list={list}
        filtered={filtered}
        emptyMessage={t("designApprovals.reviews.empty")}
        page={page}
        onPageChange={setPage}
      >
        {list.data?.items.map((review) => (
          <InboxRow
            key={review.reviewId}
            title={review.title}
            dossier={`${review.designProjectCode} · ${review.designProjectName}`}
            status={
              <Badge variant="outline" className="whitespace-nowrap">
                {t(
                  review.hasMyComment
                    ? "designApprovals.comment.done"
                    : "designApprovals.comment.pending",
                )}
              </Badge>
            }
            details={[
              [
                t("designApprovals.field.stage"),
                `${t(`designProjects.stage.${review.stage}`)} · ${t("designApprovals.field.revision", { n: review.revisionNumber })}`,
              ],
              [t("designApprovals.field.submittedBy"), review.designManagerName ?? "—"],
              [
                t("designApprovals.field.submittedAt"),
                review.submittedAt ? formatDateTime(review.submittedAt, lang) : "—",
              ],
            ]}
            submittedAt={review.submittedAt}
            action={
              <Button asChild variant="outline" size="sm">
                <Link
                  to={`/admin/design-reviews/${review.designProjectId}?reviewId=${review.reviewId}`}
                >
                  {t(
                    review.hasMyComment
                      ? "designApprovals.action.reviewComments"
                      : "designApprovals.action.comment",
                  )}
                </Link>
              </Button>
            }
          />
        ))}
      </InboxResults>
    </div>
  );
}

/** Loading, failure, first-use and no-match states around one paged list. */
function InboxResults({
  list,
  filtered,
  emptyMessage,
  page,
  onPageChange,
  children,
}: {
  list: ReturnType<typeof useApprovalList<unknown>>;
  filtered: boolean;
  emptyMessage: string;
  page: number;
  onPageChange: (page: number) => void;
  children: ReactNode;
}) {
  const { t } = useI18n();
  if (list.error) return <PageError message={list.error} onRetry={() => void list.reload()} />;
  if (!list.data) return <PageLoading />;
  if (list.data.total === 0)
    return filtered ? (
      <PageEmpty message={t("designApprovals.noMatches")} />
    ) : (
      <PageEmpty message={emptyMessage} />
    );
  return (
    <div className={cn("space-y-4", list.loading && "opacity-60")} aria-busy={list.loading}>
      <ul className="space-y-3">{children}</ul>
      <ListPagination
        page={page}
        pageSize={PAGE_SIZE}
        total={list.data.total}
        onPageChange={onPageChange}
        disabled={list.loading}
      />
    </div>
  );
}

function InboxRow({
  title,
  dossier,
  status,
  details,
  submittedAt,
  action,
}: {
  title: string;
  dossier: string;
  status?: ReactNode;
  details: [label: string, value: string][];
  submittedAt?: string | null;
  action: ReactNode;
}) {
  const { t } = useI18n();
  const waited = submittedAt ? daysWaiting(submittedAt) : null;
  return (
    <li className="rounded-lg border border-border bg-card p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h2 className="break-words text-base font-semibold">{title}</h2>
          <p className="break-words text-sm text-muted-foreground">{dossier}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {status}
          {waited != null && (
            <span
              className={cn(
                "whitespace-nowrap text-sm",
                waited >= LONG_WAIT_DAYS
                  ? "font-medium text-warning-strong"
                  : "text-muted-foreground",
              )}
            >
              {waited === 0
                ? t("designApprovals.waiting.today")
                : t("designApprovals.waiting.days", { count: waited })}
            </span>
          )}
        </div>
      </div>
      <dl className="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
        {details.map(([label, value]) => (
          <div key={label} className="min-w-0">
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className="break-words">{value}</dd>
          </div>
        ))}
      </dl>
      <div className="mt-3">{action}</div>
    </li>
  );
}
