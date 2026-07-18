import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ClipboardList, Loader2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { adminApi, type SurveyResponse } from "@/services/adminApi";
import { extractApiError } from "@/lib/apiError";

/**
 * Minimal detail page for a survey — NIH-99 ships the list view; NIH-101
 * layers the full detail (media, sync workflow, related records) on top of
 * this placeholder. The stub still renders enough context (code, location,
 * linked project / opportunity, Drive status) so the row-click flow works
 * end-to-end and the URL is deep-linkable.
 */
const AdminSurveyDetail = () => {
  const { id } = useParams<{ id: string }>();
  const surveyId = Number(id);
  const isValidId = Number.isFinite(surveyId) && surveyId > 0;
  const { t, lang } = useI18n();
  const [survey, setSurvey] = useState<SurveyResponse | null>(null);
  const [loading, setLoading] = useState(isValidId);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isValidId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    (async () => {
      try {
        const { data } = await adminApi.getSurvey(surveyId);
        if (!cancelled) setSurvey(data);
      } catch (err) {
        if (cancelled) return;
        // 404 is a normal "row is gone" — let the !survey branch render
        // the localised not-found message instead of leaking an Axios
        // string. Other errors still surface through PageError.
        const status = (err as { response?: { status?: number } })?.response?.status;
        if (status !== 404) {
          setError(extractApiError(err));
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isValidId, surveyId]);

  const formatDate = (iso?: string | null) => {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleDateString(lang);
    } catch {
      return iso;
    }
  };

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
          <PageError message={error} />
        ) : !survey ? (
          <PageError message={t("common.notFound")} />
        ) : (
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
            <div className="flex flex-wrap items-center gap-2">
              <ClipboardList className="h-5 w-5 text-slate-500" />
              <h1 className="text-xl font-semibold text-slate-900">{survey.code}</h1>
              <Badge variant="outline" className="text-xs">
                {t(`surveys.driveStatus.${survey.driveSyncStatus}`)}
              </Badge>
            </div>
            <p className="mt-1 text-sm text-slate-600">{survey.location}</p>

            <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">
                  {t("surveys.field.constructionType")}
                </dt>
                <dd className="mt-0.5 text-slate-800">
                  {survey.constructionTypeLabel ?? survey.constructionTypeCode ?? "—"}
                </dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">
                  {t("surveys.field.surveyDate")}
                </dt>
                <dd className="mt-0.5 text-slate-800">{formatDate(survey.surveyDate)}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">
                  {t("surveys.field.surveyor")}
                </dt>
                <dd className="mt-0.5 text-slate-800">{survey.surveyorName ?? "—"}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">
                  {t("surveys.field.linkedProject")}
                </dt>
                <dd className="mt-0.5 text-slate-800">{survey.linkedProjectName ?? "—"}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-slate-500">
                  {t("surveys.field.linkedOpportunity")}
                </dt>
                <dd className="mt-0.5 text-slate-800">{survey.linkedOpportunityName ?? "—"}</dd>
              </div>
              {survey.driveSyncError ? (
                <div className="sm:col-span-2">
                  <dt className="text-xs uppercase tracking-wide text-rose-600">
                    {t("surveys.driveStatus.Failed")}
                  </dt>
                  <dd className="mt-0.5 break-words text-sm text-rose-700">{survey.driveSyncError}</dd>
                </div>
              ) : null}
            </dl>

            <p className="mt-4 rounded-md border border-slate-200 bg-slate-50 p-3 text-xs text-slate-600">
              {t("surveys.detail.comingSoon")}
            </p>
          </div>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminSurveyDetail;
