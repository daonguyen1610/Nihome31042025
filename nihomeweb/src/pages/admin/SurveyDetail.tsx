import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  AlertTriangle,
  ArrowLeft,
  Calendar,
  ClipboardList,
  Cloud,
  CloudOff,
  History,
  Image as ImageIcon,
  Info,
  Loader2,
  UserRound,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  adminApi,
  type SurveyDriveSyncStatus,
  type SurveyResponse,
  type SurveyTimelineEvent,
} from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";

// ---------------------------- shared ----------------------------

const DRIVE_STATUS_BADGE: Record<SurveyDriveSyncStatus, string> = {
  NotSynced: "border-slate-200 bg-slate-50 text-slate-700",
  Syncing: "border-amber-200 bg-amber-50 text-amber-700",
  Synced: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Failed: "border-rose-200 bg-rose-50 text-rose-700",
};

/** Same look as the list's badge — kept local because SurveyDetail is a
 * placeholder page that will be replaced once NIH-101 gets its full media
 * / sync-log slice; extracting to a shared component now would be
 * premature. */
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
          <TooltipContent className="max-w-xs break-words text-xs">{error}</TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
  return badge;
};

// ---------------------------- page ----------------------------

const AdminSurveyDetail = () => {
  const { id } = useParams<{ id: string }>();
  const surveyId = Number(id);
  const isValidId = Number.isFinite(surveyId) && surveyId > 0;
  const { t, lang } = useI18n();

  const [survey, setSurvey] = useState<SurveyResponse | null>(null);
  const [loading, setLoading] = useState(isValidId);
  const [error, setError] = useState<string | null>(null);

  const [timeline, setTimeline] = useState<SurveyTimelineEvent[] | null>(null);
  const [timelineLoading, setTimelineLoading] = useState(false);

  const formatDate = useCallback(
    (iso?: string | null) => {
      if (!iso) return "—";
      try {
        return new Date(iso).toLocaleDateString(lang);
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
        return new Date(iso).toLocaleString(lang);
      } catch {
        return iso;
      }
    },
    [lang],
  );

  const fetchSurvey = useCallback(async () => {
    if (!isValidId) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.getSurvey(surveyId);
      setSurvey(data);
    } catch (err) {
      // 404 falls through to the not-found branch via a null survey; other
      // errors surface through PageError.
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status !== 404) {
        setError(extractApiError(err));
      }
    } finally {
      setLoading(false);
    }
  }, [isValidId, surveyId]);

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
    void fetchTimeline();
  }, [fetchSurvey, fetchTimeline]);

  const infoRows: [string, string][] = useMemo(() => {
    if (!survey) return [];
    return [
      [t("surveys.field.location"), survey.location],
      [
        t("surveys.field.constructionType"),
        survey.constructionTypeLabel ?? survey.constructionTypeCode ?? "—",
      ],
      [t("surveys.field.surveyDate"), formatDate(survey.surveyDate)],
      [t("surveys.field.surveyor"), survey.surveyorName ?? "—"],
      [t("surveys.field.linkedProject"), survey.linkedProjectName ?? "—"],
      [t("surveys.field.linkedOpportunity"), survey.linkedOpportunityName ?? "—"],
      [t("surveys.detail.info.createdAt"), formatDateTime(survey.createdAt)],
      [t("surveys.detail.info.updatedAt"), formatDateTime(survey.updatedAt)],
      [t("surveys.detail.info.lastSyncedAt"), formatDateTime(survey.lastSyncedAt)],
    ];
  }, [survey, t, formatDate, formatDateTime]);

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
          className="inline-flex items-center gap-1 text-sm text-slate-600 hover:text-slate-800"
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
            <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <ClipboardList className="h-5 w-5 shrink-0 text-slate-500" />
                    <h1 className="text-xl font-bold text-slate-900 md:text-2xl">{survey.code}</h1>
                    <DriveStatusBadge
                      status={survey.driveSyncStatus}
                      error={survey.driveSyncError}
                    />
                  </div>
                  <p className="mt-1 break-words text-sm text-slate-600">{survey.location}</p>
                </div>
              </div>
            </div>

            {/* Tabs */}
            <Tabs defaultValue="info" className="w-full">
              <TabsList className="w-full justify-start overflow-x-auto whitespace-nowrap rounded-lg bg-slate-100 p-1">
                <TabsTrigger value="info">
                  <Info className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.info")}
                </TabsTrigger>
                <TabsTrigger value="media">
                  <ImageIcon className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.media")}
                </TabsTrigger>
                <TabsTrigger value="timeline">
                  <History className="mr-1 h-4 w-4" />
                  {t("surveys.detail.tab.timeline")}
                </TabsTrigger>
              </TabsList>

              <TabsContent value="info" className="mt-3">
                <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
                  <dl className="grid gap-3 sm:grid-cols-2">
                    {infoRows.map(([label, value]) => (
                      <div key={label}>
                        <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">
                          {label}
                        </dt>
                        <dd className="mt-0.5 break-words text-sm text-slate-800">{value}</dd>
                      </div>
                    ))}
                  </dl>
                  {survey.note ? (
                    <div className="mt-4 border-t border-slate-100 pt-3">
                      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
                        {t("surveys.field.note")}
                      </div>
                      <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-800">
                        {survey.note}
                      </p>
                    </div>
                  ) : null}
                </div>
              </TabsContent>

              <TabsContent value="media" className="mt-3">
                <div className="rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600 shadow-sm">
                  <ImageIcon className="mx-auto mb-2 h-6 w-6 text-slate-400" />
                  {t("surveys.detail.mediaComingSoon")}
                </div>
              </TabsContent>

              <TabsContent value="timeline" className="mt-3">
                <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
                  {timelineLoading ? (
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                      <Loader2 className="h-4 w-4 animate-spin" />
                      {t("common.loading")}
                    </div>
                  ) : !timeline || timeline.length === 0 ? (
                    <p className="text-sm text-slate-500">{t("surveys.detail.timelineEmpty")}</p>
                  ) : (
                    <ol className="space-y-2">
                      {timeline.map((ev) => (
                        <li key={ev.id} className="flex items-start gap-2 text-sm">
                          <div className="mt-1 h-2 w-2 rounded-full bg-slate-300" />
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                              <Calendar className="h-3 w-3" />
                              <span>{formatDateTime(ev.occurredAt)}</span>
                              {ev.userName ? (
                                <span className="inline-flex items-center gap-1">
                                  <UserRound className="h-3 w-3" />
                                  {ev.userName}
                                </span>
                              ) : null}
                              <span className="rounded bg-slate-100 px-1.5 py-0.5 font-mono">
                                {ev.action}
                              </span>
                            </div>
                            {ev.message ? (
                              <div className="mt-0.5 break-words text-sm text-slate-700">
                                {ev.message}
                              </div>
                            ) : null}
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
