import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Briefcase, MapPin, Eye, Pencil, Trash2, ChevronDown, Users, FileDown, Mail, Clock3, ClipboardList, Sparkles } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import RecruitmentMetadataManager from "@/pages/admin/components/RecruitmentMetadataManager";
import { useRecruitmentMetadata } from "@/hooks/useContentApi";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import type { JobPositionResponse, JobApplicationResponse } from "@/services/adminApi";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

const APP_STATUS: Record<string, { bg: string; color: string }> = {
  new: { bg: "hsl(var(--admin-info-soft))", color: "hsl(var(--admin-info))" },
  interview: { bg: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))" },
  hired: { bg: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" },
  rejected: { bg: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" },
};

function getErrorMessage(error: unknown) {
  if (error && typeof error === "object" && "response" in error) {
    const r = (error as { response?: { data?: { message?: string } } }).response;
    if (r?.data?.message) return r.data.message;
  }
  return "Có lỗi xảy ra";
}

const positionActionButtonClass =
  "inline-flex items-center justify-center gap-1.5 py-2.5 px-3 rounded-xl text-xs font-bold border transition";

const applicationIconActionButtonClass =
  "inline-flex items-center justify-center w-9 h-9 rounded-lg border transition-colors";

const AdminRecruitment = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: metadata, refetch: refetchMetadata } = useRecruitmentMetadata(true);

  const [positions, setPositions] = useState<JobPositionResponse[]>([]);
  const [applications, setApplications] = useState<JobApplicationResponse[]>([]);
  const [loadingPositions, setLoadingPositions] = useState(true);
  const [loadingApps, setLoadingApps] = useState(true);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [filterPosition, setFilterPosition] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<string>("");
  const [selectedApplication, setSelectedApplication] = useState<JobApplicationResponse | null>(null);
  const employmentTypeMap = useMemo(
    () => new Map((metadata?.employmentTypes ?? []).map((item) => [item.value, item.label])),
    [metadata],
  );
  const experienceLevelMap = useMemo(
    () => new Map((metadata?.experienceLevels ?? []).map((item) => [item.value, item.label])),
    [metadata],
  );
  const applicationStatusMap = useMemo(
    () => new Map((metadata?.applicationStatuses ?? []).map((item) => [item.value, item.label])),
    [metadata],
  );
  const applicationStatusOptions = useMemo(() => {
    if ((metadata?.applicationStatuses?.length ?? 0) > 0) {
      return metadata!.applicationStatuses;
    }

    return Array.from(new Set(applications.map((item) => item.status))).map((value) => ({
      value,
      label: value,
    }));
  }, [applications, metadata]);
  const getApplicationStatusLabel = useCallback(
    (value: string) => applicationStatusMap.get(value) ?? value,
    [applicationStatusMap],
  );

  const loadPositions = useCallback(async () => {
    setLoadingPositions(true);
    try {
      const res = await adminApi.getJobPositions(true);
      setPositions(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoadingPositions(false);
    }
  }, [t, toast]);

  const loadApplications = useCallback(async () => {
    setLoadingApps(true);
    try {
      const res = await adminApi.getJobApplications(
        filterPosition !== "" ? filterPosition : undefined,
        filterStatus || undefined,
      );
      setApplications(res.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoadingApps(false);
    }
  }, [t, toast, filterPosition, filterStatus]);

  useEffect(() => { loadPositions(); }, [loadPositions]);
  useEffect(() => { loadApplications(); }, [loadApplications]);

  const deletePosition = async (id: number, title: string) => {
    if (!confirm(t("recruit.messages.confirmDeletePosition", { title }))) return;
    setDeletingId(id);
    try {
      await adminApi.deleteJobPosition(id);
      toast({ title: t("recruit.messages.deleted"), description: title });
      loadPositions();
      loadApplications();
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    } finally {
      setDeletingId(null);
    }
  };

  const updateAppStatus = async (id: number, status: string) => {
    try {
      await adminApi.updateApplicationStatus(id, status);
      setApplications((prev) => prev.map((a) => (a.id === id ? { ...a, status } : a)));
      toast({ title: t("recruit.messages.updated"), description: getApplicationStatusLabel(status) });
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    }
  };

  const deleteApp = async (id: number) => {
    if (!confirm(t("recruit.messages.confirmDeleteApplication"))) return;
    try {
      await adminApi.deleteApplication(id);
      setApplications((prev) => prev.filter((a) => a.id !== id));
      toast({ title: t("recruit.messages.deleted") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  const activeCount = useMemo(() => positions.filter((p) => p.isActive).length, [positions]);
  const newApplicationsCount = useMemo(
    () => applications.filter((application) => application.status === "new").length,
    [applications],
  );
  const interviewApplicationsCount = useMemo(
    () => applications.filter((application) => application.status === "interview").length,
    [applications],
  );
  const hiredApplicationsCount = useMemo(
    () => applications.filter((application) => application.status === "hired").length,
    [applications],
  );
  const summaryCards = useMemo(
    () => [
      {
        label: t("recruit.summary.openPositions"),
        value: activeCount,
        hint: t("recruit.summary.totalPositions", { count: positions.length }),
        icon: Briefcase,
        bg: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(18 92% 60%))",
      },
      {
        label: t("recruit.summary.newApplications"),
        value: newApplicationsCount,
        hint: t("recruit.summary.totalApplications", { count: applications.length }),
        icon: ClipboardList,
        bg: "linear-gradient(135deg, hsl(195 85% 50%), hsl(214 88% 60%))",
      },
      {
        label: t("recruit.summary.interviewing"),
        value: interviewApplicationsCount,
        hint: t("recruit.summary.interviewHint"),
        icon: Clock3,
        bg: "linear-gradient(135deg, hsl(40 95% 55%), hsl(18 92% 60%))",
      },
      {
        label: t("recruit.summary.hired"),
        value: hiredApplicationsCount,
        hint: t("recruit.summary.hiredHint"),
        icon: Sparkles,
        bg: "linear-gradient(135deg, hsl(145 60% 42%), hsl(165 62% 46%))",
      },
    ],
    [activeCount, applications.length, hiredApplicationsCount, interviewApplicationsCount, newApplicationsCount, positions.length, t],
  );

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("recruit.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("recruit.headerStats", { positions: activeCount, applications: applications.length })}
          </p>
        </div>
        <Link
          to="/admin/recruitment/new"
          className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
        >
          <Plus className="w-4 h-4" /> {t("recruit.postPosition")}
        </Link>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-5 mb-8">
        {summaryCards.map((card) => (
          <div
            key={card.label}
            className="relative overflow-hidden rounded-[28px] p-6 text-white shadow-[0_18px_38px_-24px_rgba(15,23,42,0.7)]"
            style={{ background: card.bg }}
          >
            <div className="absolute -right-8 -top-8 w-28 h-28 rounded-full bg-white/10" />
            <div className="relative z-10 flex items-start justify-between">
              <div>
                <p className="text-[11px] uppercase tracking-[0.24em] text-white/70 font-bold">{card.label}</p>
                <p className="mt-4 font-display text-4xl font-extrabold leading-none">{card.value}</p>
                <p className="mt-2 text-sm text-white/80">{card.hint}</p>
              </div>
              <div className="w-12 h-12 rounded-2xl bg-white/15 backdrop-blur flex items-center justify-center">
                <card.icon className="w-5 h-5" strokeWidth={1.75} />
              </div>
            </div>
          </div>
        ))}
      </div>

      <RecruitmentMetadataManager onUpdated={refetchMetadata} />

      <div className="flex items-end justify-between gap-4 mb-4">
        <div>
          <h2 className="font-display text-xl font-extrabold">{t("recruit.positions")}</h2>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("recruit.positionsOverview")}
          </p>
        </div>
      </div>
      {loadingPositions ? (
        <div className="flex justify-center py-10">
          <div className="w-7 h-7 border-4 rounded-full animate-spin" style={{ borderColor: "hsl(var(--admin-primary))", borderTopColor: "transparent" }} />
        </div>
      ) : positions.length === 0 ? (
        <div className="admin-card p-10 text-center mb-10" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("recruit.emptyPositions")}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5 mb-10">
          {positions.map((p) => (
            <div key={p.id} className="admin-card p-6 flex flex-col relative overflow-hidden">
              <div className="absolute inset-x-0 top-0 h-1.5" style={{ background: p.isActive ? "linear-gradient(90deg, hsl(var(--admin-success)), hsl(160 65% 50%))" : "linear-gradient(90deg, hsl(var(--admin-danger)), hsl(18 92% 60%))" }} />
              <div className="flex items-start justify-between mb-4">
                <div
                  className="w-11 h-11 rounded-2xl flex items-center justify-center text-white"
                  style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                >
                  <Briefcase className="w-5 h-5" strokeWidth={1.75} />
                </div>
                <span
                  className="admin-chip"
                  style={
                    p.isActive
                      ? { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                      : { background: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" }
                  }
                >
                  {p.isActive ? t("recruit.hiring") : t("recruit.closed")}
                </span>
              </div>
              <h3 className="font-display text-lg font-extrabold mb-0.5">{p.title}</h3>
              <p className="text-xs mb-2" style={{ color: "hsl(var(--admin-muted))" }}>{p.department}</p>
              {p.description && (
                <p className="text-sm leading-relaxed line-clamp-3 mb-4" style={{ color: "hsl(var(--admin-muted))" }}>
                  {p.description}
                </p>
              )}
              <div className="flex flex-wrap gap-2 mb-4">
                <span className="admin-chip text-xs">
                  {employmentTypeMap.get(p.employmentType) ?? p.employmentType}
                </span>
                <span className="admin-chip text-xs">
                  {experienceLevelMap.get(p.experienceLevel) ?? p.experienceLevel}
                </span>
              </div>
              {p.requirements.length > 0 && (
                <div className="rounded-2xl p-4 mb-4" style={{ background: "hsl(var(--admin-bg))" }}>
                  <p className="text-[11px] uppercase tracking-[0.18em] font-bold mb-2" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("recruit.featuredRequirements")}
                  </p>
                  <ul className="space-y-1.5 text-sm">
                    {p.requirements.slice(0, 3).map((requirement, index) => (
                      <li key={`${p.id}-${index}`} className="flex gap-2">
                        <span style={{ color: "hsl(var(--admin-primary))" }}>•</span>
                        <span className="line-clamp-1">{requirement}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
              <div className="flex items-center justify-between mt-auto pt-4 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <span className="text-xs flex items-center gap-1.5" style={{ color: "hsl(var(--admin-muted))" }}>
                  <MapPin className="w-3 h-3" /> {p.location}
                </span>
                <span className="text-xs font-bold flex items-center gap-1" style={{ color: "hsl(var(--admin-primary))" }}>
                  <Users className="w-3 h-3" /> {t("recruit.applicationCount", { count: p.applicationCount })}
                </span>
              </div>
              <div className="flex gap-2 mt-3">
                <Link
                  to={`/admin/recruitment/${p.id}/edit`}
                  className={`flex-1 ${positionActionButtonClass}`}
                  style={{
                    borderColor: "hsl(var(--admin-border))",
                    background: "hsl(var(--admin-bg))",
                    color: "hsl(var(--admin-primary))",
                  }}
                >
                  <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
                </Link>
                <button
                  onClick={() => deletePosition(p.id, p.title)}
                  disabled={deletingId === p.id}
                  className={`flex-1 ${positionActionButtonClass} disabled:opacity-50`}
                  style={{
                    borderColor: "hsl(var(--admin-border))",
                    background: "hsl(var(--admin-danger-soft))",
                    color: "hsl(var(--admin-danger))",
                  }}
                >
                  <Trash2 className="w-3.5 h-3.5" /> {t("recruit.deletePosition")}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="admin-card p-5 mb-4">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
          <h2 className="font-display text-xl font-extrabold">{t("recruit.applications")}</h2>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("recruit.applicationsOverview")}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <span className="admin-chip text-xs" style={{ background: "hsl(var(--admin-info-soft))", color: "hsl(var(--admin-info))" }}>
              {t("recruit.summary.newStatusCount", { count: newApplicationsCount })}
            </span>
            <span className="admin-chip text-xs" style={{ background: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))" }}>
              {t("recruit.summary.interviewStatusCount", { count: interviewApplicationsCount })}
            </span>
            <span className="admin-chip text-xs" style={{ background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }}>
              {t("recruit.summary.hiredStatusCount", { count: hiredApplicationsCount })}
            </span>
          </div>
        </div>
        <div className="flex flex-col sm:flex-row gap-2 mt-4">
          <div className="relative">
            <select
              className="admin-input pr-8 text-sm appearance-none"
              value={filterPosition}
              onChange={(e) => setFilterPosition(e.target.value === "" ? "" : Number(e.target.value))}
            >
              <option value="">{t("recruit.filters.allPositions")}</option>
              {positions.map((p) => (
                <option key={p.id} value={p.id}>{p.title}</option>
              ))}
            </select>
            <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
          </div>
          <div className="relative">
            <select
              className="admin-input pr-8 text-sm appearance-none"
              value={filterStatus}
              onChange={(e) => setFilterStatus(e.target.value)}
            >
              <option value="">{t("recruit.filters.allStatuses")}</option>
              {applicationStatusOptions.map((statusOption) => (
                <option key={statusOption.value} value={statusOption.value}>{statusOption.label}</option>
              ))}
            </select>
            <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
          </div>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        {loadingApps ? (
          <div className="flex justify-center py-10">
            <div className="w-7 h-7 border-4 rounded-full animate-spin" style={{ borderColor: "hsl(var(--admin-primary))", borderTopColor: "transparent" }} />
          </div>
        ) : applications.length === 0 ? (
          <div className="p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("recruit.emptyApplications")}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 gap-4 p-4 lg:hidden">
              {applications.map((a) => {
                const st = APP_STATUS[a.status] ?? APP_STATUS.new;
                return (
                  <div key={a.id} className="rounded-3xl border p-4" style={{ borderColor: "hsl(var(--admin-border))" }}>
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-start gap-3 min-w-0">
                        <div
                          className="w-11 h-11 rounded-2xl text-white flex items-center justify-center text-sm font-bold shrink-0"
                          style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                        >
                          {a.candidateName[0]?.toUpperCase()}
                        </div>
                        <div className="min-w-0">
                          <p className="font-semibold">{a.candidateName}</p>
                          <p className="text-xs truncate" style={{ color: "hsl(var(--admin-muted))" }}>{a.positionTitle}</p>
                        </div>
                      </div>
                      <span className="admin-chip text-xs" style={{ background: st.bg, color: st.color }}>
                        {getApplicationStatusLabel(a.status)}
                      </span>
                    </div>

                    <div className="grid grid-cols-1 gap-2 mt-4 text-sm">
                      <p className="flex items-center gap-2" style={{ color: "hsl(var(--admin-muted))" }}>
                        <Mail className="w-4 h-4" /> {a.email}
                      </p>
                      <p className="flex items-center gap-2" style={{ color: "hsl(var(--admin-muted))" }}>
                        <Clock3 className="w-4 h-4" /> {new Date(a.appliedAt).toLocaleDateString("vi-VN")}
                      </p>
                      <p style={{ color: "hsl(var(--admin-muted))" }}>
                        {t("recruit.experienceYearsLabel")}: {a.experienceYears != null ? t("recruit.experienceYearsValue", { count: a.experienceYears }) : "—"}
                      </p>
                    </div>

                    <div className="flex flex-wrap gap-2 mt-4">
                      {a.coverLetter && (
                        <button
                          onClick={() => setSelectedApplication(a)}
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-bold border hover:bg-muted"
                          style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-info))" }}
                        >
                          <Eye className="w-3.5 h-3.5" /> {t("recruit.viewCoverLetter")}
                        </button>
                      )}
                      {a.cvUrl && (
                        <a
                          href={`${import.meta.env.VITE_API_URL ?? ""}${a.cvUrl}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-bold border hover:bg-muted"
                          style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-primary))" }}
                        >
                          <FileDown className="w-3.5 h-3.5" /> {t("recruit.downloadCv")}
                        </a>
                      )}
                    </div>

                    <div className="grid grid-cols-[minmax(0,1fr)_auto] gap-2 mt-4">
                      <div className="relative">
                        <select
                          className="admin-input pr-8 text-sm appearance-none w-full"
                          value={a.status}
                          onChange={(e) => updateAppStatus(a.id, e.target.value)}
                        >
                          {applicationStatusOptions.map((statusOption) => (
                            <option key={statusOption.value} value={statusOption.value}>{statusOption.label}</option>
                          ))}
                        </select>
                        <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
                      </div>
                      <button
                        onClick={() => deleteApp(a.id)}
                        className="inline-flex items-center justify-center gap-1 py-2 rounded-lg text-xs font-bold border"
                        style={{ borderColor: "hsl(var(--admin-border))", background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-danger))" }}
                      >
                        <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="hidden lg:block overflow-x-auto">
            <table className="w-full text-sm">
              <thead style={{ background: "hsl(var(--admin-bg))" }}>
                <tr className="text-left">
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.candidate")}</th>
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.position")}</th>
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.experience")}</th>
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("recruit.appliedOn")}</th>
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider">{t("common.status")}</th>
                  <th className="px-6 py-4 font-bold text-xs uppercase tracking-wider text-right">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {applications.map((a) => {
                  const st = APP_STATUS[a.status] ?? APP_STATUS.new;
                  return (
                    <tr key={a.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div
                            className="w-9 h-9 rounded-full text-white flex items-center justify-center text-sm font-bold shrink-0"
                            style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(255 80% 72%))" }}
                          >
                            {a.candidateName[0]?.toUpperCase()}
                          </div>
                          <div>
                            <p className="font-semibold">{a.candidateName}</p>
                            <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{a.email}</p>
                            {a.phone && <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{a.phone}</p>}
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4 font-medium">{a.positionTitle}</td>
                      <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                        {a.experienceYears != null ? t("recruit.experienceYearsValue", { count: a.experienceYears }) : "—"}
                      </td>
                      <td className="px-6 py-4" style={{ color: "hsl(var(--admin-muted))" }}>
                        {new Date(a.appliedAt).toLocaleDateString("vi-VN")}
                      </td>
                      <td className="px-6 py-4">
                        <div className="relative inline-block">
                          <select
                            className="admin-chip pr-6 appearance-none cursor-pointer text-xs"
                            value={a.status}
                            onChange={(e) => updateAppStatus(a.id, e.target.value)}
                            style={{ background: st.bg, color: st.color }}
                          >
                            {applicationStatusOptions.map((statusOption) => (
                              <option key={statusOption.value} value={statusOption.value}>{statusOption.label}</option>
                            ))}
                          </select>
                          <ChevronDown className="absolute right-1 top-1/2 -translate-y-1/2 w-3 h-3 pointer-events-none" style={{ color: st.color }} />
                        </div>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex justify-end">
                          <div className="inline-flex items-center flex-wrap justify-end gap-1.5 max-w-[220px]">
                          {a.coverLetter && (
                            <button
                              onClick={() => setSelectedApplication(a)}
                              title={t("recruit.viewCoverLetter")}
                              aria-label={t("recruit.viewCoverLetter")}
                              className={applicationIconActionButtonClass}
                              style={{
                                borderColor: "hsl(var(--admin-info-soft))",
                                background: "hsl(var(--admin-info-soft) / 0.35)",
                                color: "hsl(var(--admin-info))",
                              }}
                            >
                              <Eye className="w-3.5 h-3.5" />
                            </button>
                          )}
                          {a.cvUrl && (
                            <a
                              href={`${import.meta.env.VITE_API_URL ?? ""}${a.cvUrl}`}
                              target="_blank"
                              rel="noopener noreferrer"
                              title={t("recruit.downloadCv")}
                              aria-label={t("recruit.downloadCv")}
                              className={applicationIconActionButtonClass}
                              style={{
                                borderColor: "hsl(var(--admin-border))",
                                background: "hsl(var(--admin-bg))",
                                color: "hsl(var(--admin-primary))",
                              }}
                            >
                              <FileDown className="w-3.5 h-3.5" />
                            </a>
                          )}
                          <button
                            onClick={() => deleteApp(a.id)}
                            title={t("common.delete")}
                            className={applicationIconActionButtonClass}
                            style={{
                              borderColor: "hsl(var(--admin-border))",
                              background: "hsl(var(--admin-bg))",
                              color: "hsl(var(--admin-danger))",
                            }}
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                          </div>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            </div>
          </>
        )}
      </div>

      <Dialog open={selectedApplication !== null} onOpenChange={(open) => !open && setSelectedApplication(null)}>
        <DialogContent className="sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("recruit.coverLetterTitle")}</DialogTitle>
            <DialogDescription>
              {selectedApplication ? `${selectedApplication.candidateName} • ${selectedApplication.positionTitle}` : ""}
            </DialogDescription>
          </DialogHeader>
          <div className="rounded-2xl p-5 whitespace-pre-wrap leading-relaxed max-h-[60vh] overflow-y-auto" style={{ background: "hsl(var(--admin-bg))" }}>
            {selectedApplication?.coverLetter || t("recruit.emptyCoverLetter")}
          </div>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AdminRecruitment;
