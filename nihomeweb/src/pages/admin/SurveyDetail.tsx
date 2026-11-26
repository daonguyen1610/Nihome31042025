import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  Calendar,
  ClipboardList,
  Cloud,
  CloudOff,
  History,
  Image as ImageIcon,
  Info,
  Loader2,
  Route,
  UserRound,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import {
  adminApi,
  type SurveyDriveSyncStatus,
  type SurveyResponse,
  type SurveyTimelineEvent,
} from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { SurveyStatusBadge } from "@/components/admin/SurveyStatus";
import { surveyProgress, surveyTimeLabel } from "@/lib/surveyStatus";
import SurveyMediaPanel from "./SurveyMediaPanel";
import SurveyConditionsPanel from "./SurveyConditionsPanel";
import {
  formatDate as formatDateInZone,
  formatDateTime as formatDateTimeInZone,
  todayIsoDate,
} from "@/lib/displaySettings";

// ---------------------------- shared ----------------------------

const DRIVE_STATUS_BADGE: Record<SurveyDriveSyncStatus, string> = {
  NotSynced: "border-border bg-muted/30 text-foreground",
  Syncing: "border-warning/40 bg-warning-soft text-warning-strong",
  Synced: "border-success/30 bg-success-soft text-success-strong",
  Failed: "border-destructive/20 bg-destructive/10 text-destructive",
};

/** Same look as the list's badge — kept local because SurveyDetail is a
 * placeholder page that will be replaced once NIH-101 gets its full media
 * / sync-log slice; extracting to a shared component now would be
 * premature. */
/** Audit actions with a readable label; anything else reads as a generic change. */
const TIMELINE_ACTIONS = new Set([
  "survey.create",
  "survey.update",
  "survey.complete",
  "survey.reopen",
  "survey.media.add",
  "survey.media.remove",
  "survey.media.retry",
  "survey.checklist.update",
  "survey.conditions.import",
  "survey.conditions.replace",
]);

const DriveStatusBadge = ({
  status,
  error,
}: {
  status: SurveyDriveSyncStatus;
  error?: string | null;
}) => {
  const { t } = useI18n();
  const Icon =
    status === "Synced"
      ? Cloud
      : status === "Syncing"
        ? Loader2
        : status === "Failed"
          ? AlertTriangle
          : CloudOff;

  const badge = (
    <Badge variant="outline" className={cn("gap-1 whitespace-nowrap", DRIVE_STATUS_BADGE[status])}>
      <Icon className={cn("h-3 w-3", status === "Syncing" && "animate-spin")} />
      {t(`surveys.driveStatus.${status}`)}
    </Badge>
  );

  if (status === "Failed" && error) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span>{badge}</span>
          </TooltipTrigger>
          <TooltipContent className="max-w-xs break-words text-xs">{t(error)}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
  return badge;
};

// ---------------------------- page ----------------------------

const AdminSurveyDetail = () => {
  const uiError = useUiError();
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const surveyId = Number(id);
  const isValidId = Number.isFinite(surveyId) && surveyId > 0;
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const canManage = has("crm.surveys.manage");

  const [survey, setSurvey] = useState<SurveyResponse | null>(null);
  const [loading, setLoading] = useState(isValidId);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState("info");
  const surveyLoadedRef = useRef(false);

  const { toast } = useToast();
  const [statusBusy, setStatusBusy] = useState(false);
  const [timeline, setTimeline] = useState<SurveyTimelineEvent[] | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);

  const formatDate = useCallback(
    (iso?: string | null) => {
      if (!iso) return "—";
      try {
        return formatDateInZone(iso, lang);
      } catch {
        return iso;
      }
    },
    [lang],
  );

  const formatDateTime = useCallback(
    (iso?: string | null) => {
      if (!iso) return "—";
      try {
        return formatDateTimeInZone(iso, lang);
      } catch {
        return iso;
      }
    },
    [lang],
  );

  const fetchSurvey = useCallback(async () => {
    if (!isValidId) return;
    if (!surveyLoadedRef.current) setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getSurvey(surveyId);
      setSurvey(data);
      surveyLoadedRef.current = true;
    } catch (err) {
      // 404 falls through to the not-found branch via a null survey; other
      // errors surface through PageError.
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status !== 404) {
        setError(uiError(err));
      }
    } finally {
      setLoading(false);
    }
  }, [isValidId, surveyId, uiError]);

  const fetchTimeline = useCallback(async () => {
    if (!isValidId) return;
    setTimelineLoading(true);
    try {
      const { data } = await adminApi.getSurveyTimeline(surveyId, 100);
      setTimeline(data);
    } catch {
      // Timeline is a soft dependency — the tab shows an empty-state on
      // failure rather than breaking the whole detail page.
      setTimeline([]);
    } finally {
      setTimelineLoading(false);
    }
  }, [isValidId, surveyId]);

  useEffect(() => {
    void fetchSurvey();
  }, [fetchSurvey]);

  // Audit entries are written shortly after each change, so read the history
  // whenever the tab is opened rather than once with the page.
  useEffect(() => {
    if (activeTab === "timeline") void fetchTimeline();
  }, [activeTab, fetchTimeline]);

  /** Records that the survey took place, or reopens one recorded by mistake. */
  const setCompleted = async (completed: boolean) => {
    if (!survey) return;
    setStatusBusy(true);
    try {
      const { data } = completed
        ? await adminApi.completeSurvey(survey.id)
        : await adminApi.reopenSurvey(survey.id);
      setSurvey(data);
      toast({
        title: t(completed ? "surveys.status.completedToast" : "surveys.status.reopenedToast"),
        description: completed ? t("surveys.status.completedNext") : undefined,
      });
    } catch (err) {
      toast({ variant: "destructive", title: t("common.error"), description: uiError(err) });
    } finally {
      setStatusBusy(false);
    }
  };

  const requestedMediaId = useMemo(() => {
    const value = searchParams.get("mediaId");
    if (!value || !/^[1-9]\d*$/.test(value)) return null;
    const parsed = Number(value);
    return Number.isSafeInteger(parsed) ? parsed : null;
  }, [searchParams]);

  const targetMediaId = useMemo(
    () =>
      requestedMediaId != null && survey?.media.some((media) => media.id === requestedMediaId)
        ? requestedMediaId
        : null,
    [requestedMediaId, survey],
  );

  useEffect(() => {
    if (targetMediaId != null) setActiveTab("media");
  }, [targetMediaId]);

  const infoRows: [string, string][] = survey
    ? [
        [t("surveys.field.location"), survey.location],
        [
          t("surveys.field.constructionType"),
          survey.constructionTypeLabel ?? survey.constructionTypeCode ?? "—",
        ],
        [
          t("surveys.field.surveyDate"),
          [formatDate(survey.surveyDate), surveyTimeLabel(survey.surveyTime)]
            .filter(Boolean)
            .join(" · "),
        ],
        [
          t("surveys.field.status"),
          survey.status === "Completed"
            ? survey.completedByName
              ? t("surveys.status.completedBy", {
                  name: survey.completedByName,
                  at: formatDateTime(survey.completedAt),
                })
              : t("surveys.status.completedOn", { at: formatDate(survey.completedAt) })
            : t(`surveys.progress.${surveyProgress(survey, todayIsoDate())}`),
        ],
        [t("surveys.field.leadSurveyor"), survey.surveyorName ?? "—"],
        [
          t("surveys.field.participants"),
          survey.participants.map((participant) => participant.fullName).join(", ") || "—",
        ],
        [t("surveys.field.linkedProject"), survey.linkedProjectName ?? "—"],
        [t("surveys.field.linkedOpportunity"), survey.linkedOpportunityName ?? "—"],
        [t("surveys.field.operationalProject"), survey.operationalProjectName ?? "—"],
        [t("surveys.detail.info.createdAt"), formatDateTime(survey.createdAt)],
        [t("surveys.detail.info.updatedAt"), formatDateTime(survey.updatedAt)],
        [t("surveys.detail.info.lastSyncedAt"), formatDateTime(survey.lastSyncedAt)],
      ]
    : [];

  if (!isValidId) {
    return (
      <AdminLayout>
        <div className="p-4">
          <PageError message={t("common.notFound")} />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <Link
          to="/admin/surveys"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          {t("surveys.detail.back")}
        </Link>

        {loading ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            {t("common.loading")}
          </div>
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchSurvey()} />
        ) : !survey ? (
          <PageError message={t("common.notFound")} />
        ) : (
          <>
            {/* Header */}
            <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <ClipboardList className="h-5 w-5 shrink-0 text-muted-foreground" />
                    <h1 className="text-xl font-bold text-foreground md:text-2xl">{survey.code}</h1>
                    <SurveyStatusBadge survey={survey} />
                    <DriveStatusBadge
                      status={survey.driveSyncStatus}
                      error={survey.driveSyncError}
                    />
                  </div>
                  <p className="mt-1 break-words text-sm text-muted-foreground">
                    {survey.location}
                  </p>
                </div>
                {canManage ? (
                  survey.status === "Completed" ? (
                    <Button
                      variant="outline"
                      className="min-h-10"
                      disabled={statusBusy}
                      onClick={() => void setCompleted(false)}
                    >
                      {statusBusy ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : null}
                      {t("surveys.status.reopen")}
                    </Button>
                  ) : (
                    <Button
                      className="min-h-10"
                      disabled={statusBusy}
                      onClick={() => void setCompleted(true)}
                    >
                      {statusBusy ? (
                        <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
                      ) : (
                        <CheckCircle2 className="mr-1.5 h-4 w-4" />
                      )}
                      {t("surveys.status.complete")}
                    </Button>
                  )
                ) : null}
              </div>
              {surveyProgress(survey, todayIsoDate()) === "overdue" ? (
                <p
                  role="status"
                  className="mt-3 flex items-start gap-2 rounded-md border border-warning/40 bg-warning-soft p-3 text-sm text-amber-900"
                >
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
                  {t(
                    canManage ? "surveys.status.overdueHint" : "surveys.status.overdueHintReadOnly",
                    {
                      date: formatDate(survey.surveyDate),
                    },
                  )}
                </p>
              ) : null}
            </div>

            {/* Tabs */}
            <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
              <TabsList className="w-full justify-start overflow-x-auto whitespace-nowrap rounded-lg bg-muted p-1">
                <TabsTrigger value="info">
                  <Info className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.info")}
                </TabsTrigger>
                <TabsTrigger value="media" data-testid="survey-media-tab">
                  <ImageIcon className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.media")}
                </TabsTrigger>
                <TabsTrigger value="conditions" data-testid="survey-conditions-tab">
                  <Route className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.conditions")}
                </TabsTrigger>
                <TabsTrigger value="timeline">
                  <History className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.timeline")}
                </TabsTrigger>
              </TabsList>

              <TabsContent value="info" className="mt-3">
                <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
                  <dl className="grid gap-3 sm:grid-cols-2">
                    {infoRows.map(([label, value]) => (
                      <div key={label}>
                        <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                          {label}
                        </dt>
                        <dd className="mt-0.5 break-words text-sm text-foreground">{value}</dd>
                      </div>
                    ))}
                  </dl>
                  {survey.note ? (
                    <div className="mt-4 border-t border-border pt-3">
                      <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                        {t("surveys.field.note")}
                      </div>
                      <p className="mt-1 whitespace-pre-wrap break-words text-sm text-foreground">
                        {survey.note}
                      </p>
                    </div>
                  ) : null}
                </div>
              </TabsContent>

              <TabsContent value="media" className="mt-3">
                <SurveyMediaPanel
                  survey={survey}
                  canManage={canManage}
                  onRefresh={fetchSurvey}
                  formatDateTime={formatDateTime}
                  targetMediaId={targetMediaId}
                />
              </TabsContent>

              <TabsContent value="conditions" className="mt-3">
                <SurveyConditionsPanel
                  survey={survey}
                  canManage={canManage}
                  onRefresh={fetchSurvey}
                />
              </TabsContent>

              <TabsContent value="timeline" className="mt-3">
                <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
                  {timelineLoading ? (
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                      <Loader2 className="h-4 w-4 animate-spin" />
                      {t("common.loading")}
                    </div>
                  ) : !timeline || timeline.length === 0 ? (
                    <p className="text-sm text-muted-foreground">
                      {t("surveys.detail.timelineEmpty")}
                    </p>
                  ) : (
                    <ol className="space-y-2">
                      {timeline.map((ev) => (
                        <li key={ev.id} className="flex items-start gap-2 text-sm">
                          <div className="mt-1 h-2 w-2 rounded-full bg-border" />
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                              <Calendar className="h-3 w-3" />
                              <span>{formatDateTime(ev.occurredAt)}</span>
                              {ev.userName ? (
                                <span className="inline-flex items-center gap-1">
                                  <UserRound className="h-3 w-3" />
                                  {ev.userName}
                                </span>
                              ) : null}
                            </div>
                            <div className="mt-0.5 break-words text-sm text-foreground">
                              {t(
                                TIMELINE_ACTIONS.has(ev.action)
                                  ? `surveys.timeline.${ev.action}`
                                  : "surveys.timeline.other",
                              )}
                            </div>
                          </div>
                        </li>
                      ))}
                    </ol>
                  )}
                </div>
              </TabsContent>
            </Tabs>
          </>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminSurveyDetail;
