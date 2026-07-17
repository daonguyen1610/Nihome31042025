import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Plus, Briefcase, MapPin, Eye, CheckCircle2, X, Pencil, Trash2, Users, FileDown, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { adminApi } from "@/services/adminApi";
import type { JobPositionResponse, JobApplicationResponse, EmploymentTypeResponse } from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { matchesSearch } from "@/lib/utils";

const EMP_TYPE_KEY_MAP: Record<string, string> = {
  "full-time": "rec.empType.fullTime",
  "part-time": "rec.empType.partTime",
  "intern": "rec.empType.intern",
};

function getEmploymentTypeLabel(code: string, fallback: string, t: (key: string) => string) {
  const key = EMP_TYPE_KEY_MAP[code];
  if (key) return t(key);
  return fallback || code;
}

function getExperienceLabel(code: string, t: (key: string) => string) {
  switch (code) {
    case "student": return t("rec.exp.student");
    case "junior": return t("rec.exp.junior");
    case "mid": return t("rec.exp.mid");
    case "senior": return t("rec.exp.senior");
    default: return code;
  }
}

const APP_STATUS_STYLES: Record<string, string> = {
  new: "border-sky-200 bg-sky-50 text-sky-700",
  interview: "border-amber-200 bg-amber-50 text-amber-700",
  hired: "border-green-300 bg-green-100 text-green-800",
  rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

const APP_STATUS_DOT: Record<string, string> = {
  new: "bg-sky-500",
  interview: "bg-amber-500",
  hired: "bg-green-600",
  rejected: "bg-rose-500",
};

function getErrorMessage(error: unknown) {
  if (error && typeof error === "object" && "response" in error) {
    const r = (error as { response?: { data?: { detail?: string; message?: string } } }).response;
    if (r?.data?.detail) return r.data.detail;
    if (r?.data?.message) return r.data.message;
  }
  return undefined;
}

const AdminRecruitment = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [positions, setPositions] = useState<JobPositionResponse[]>([]);
  const [applications, setApplications] = useState<JobApplicationResponse[]>([]);
  const [loadingPositions, setLoadingPositions] = useState(true);
  const [loadingApps, setLoadingApps] = useState(true);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [employmentTypes, setEmploymentTypes] = useState<EmploymentTypeResponse[]>([]);
  const [filterPosition, setFilterPosition] = useState<number | "">("");
  const [filterStatus, setFilterStatus] = useState<string>("");
  const [positionQuery, setPositionQuery] = useState("");
  const [appQuery, setAppQuery] = useState("");

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
  useEffect(() => {
    adminApi.getEmploymentTypes(true)
      .then((res) => setEmploymentTypes(res.data))
      .catch(() => setEmploymentTypes([]));
  }, []);

  const employmentTypeMap = useMemo(() => {
    return new Map(employmentTypes.map((item) => [item.code, item.name]));
  }, [employmentTypes]);

  const filteredPositions = useMemo(() => {
    if (!positionQuery.trim()) return positions;
    return positions.filter((p) =>
      matchesSearch(p.title, positionQuery) ||
      matchesSearch(p.department, positionQuery) ||
      matchesSearch(p.location, positionQuery),
    );
  }, [positions, positionQuery]);

  const filteredApplications = useMemo(() => {
    if (!appQuery.trim()) return applications;
    return applications.filter((a) =>
      matchesSearch(a.candidateName, appQuery) ||
      matchesSearch(a.email, appQuery) ||
      matchesSearch(a.phone, appQuery) ||
      matchesSearch(a.positionTitle, appQuery),
    );
  }, [applications, appQuery]);

  const deletePosition = async (id: number, title: string) => {
    if (!confirm(t("recruit.deletePositionConfirm").replace("{title}", title))) return;
    setDeletingId(id);
    try {
      await adminApi.deleteJobPosition(id);
      toast({ title: t("recruit.deleted"), description: title });
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
      toast({ title: t("recruit.updated"), description: t(`recruit.status.${status}` as Parameters<typeof t>[0]) });
    } catch (error) {
      toast({ title: t("common.error"), description: getErrorMessage(error), variant: "destructive" });
    }
  };

  const deleteApp = async (id: number) => {
    if (!confirm(t("recruit.deleteAppConfirm"))) return;
    try {
      await adminApi.deleteApplication(id);
      setApplications((prev) => prev.filter((a) => a.id !== id));
      toast({ title: t("recruit.deleted") });
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  // Bulk selection for positions
  const positionVisibleIds = useMemo(() => filteredPositions.map((p) => p.id), [filteredPositions]);
  const {
    selectedIds: posSelectedIds,
    bulkDeleting: posBulkDeleting,
    allVisibleSelected: posAllSelected,
    someVisibleSelected: posSomeSelected,
    toggleAllVisible: posToggleAll,
    toggleOne: posToggleOne,
    clearSelection: posClearSelection,
    handleBulkDelete: posBulkDelete,
  } = useBulkSelection<number>({
    visibleIds: positionVisibleIds,
    deleteOne: (id) => adminApi.deleteJobPosition(id),
    onAfter: async () => {
      await loadPositions();
      await loadApplications();
    },
  });
  useEffect(() => {
    posClearSelection();
  }, [positionQuery, posClearSelection]);

  // Bulk selection for applications
  const appVisibleIds = useMemo(() => filteredApplications.map((a) => a.id), [filteredApplications]);
  const {
    selectedIds: appSelectedIds,
    bulkDeleting: appBulkDeleting,
    allVisibleSelected: appAllSelected,
    someVisibleSelected: appSomeSelected,
    toggleAllVisible: appToggleAll,
    toggleOne: appToggleOne,
    clearSelection: appClearSelection,
    handleBulkDelete: appBulkDelete,
  } = useBulkSelection<number>({
    visibleIds: appVisibleIds,
    deleteOne: (id) => adminApi.deleteApplication(id),
    onAfter: async () => {
      await loadApplications();
    },
  });
  useEffect(() => {
    appClearSelection();
  }, [appQuery, filterPosition, filterStatus, appClearSelection]);

  const activeCount = useMemo(() => positions.filter((p) => p.isActive).length, [positions]);

  const handleExportApplications = () => {
    downloadCsv({
      filename: createCsvFilename("admin-recruitment-applications"),
      columns: [
        { header: "ID", value: "id" },
        { header: t("recruit.candidate"), value: "candidateName" },
        { header: "Email", value: "email" },
        { header: "Phone", value: (row) => row.phone ?? "" },
        { header: t("recruit.position"), value: "positionTitle" },
        { header: t("common.status"), value: (row) => t(`recruit.status.${row.status}` as Parameters<typeof t>[0]) },
        {
          header: t("recruit.experience"),
          value: (row) => (row.experienceYears != null ? String(row.experienceYears) : ""),
        },
        { header: t("recruit.appliedOn"), value: (row) => new Date(row.appliedAt).toLocaleString("vi-VN") },
        { header: "CV URL", value: (row) => row.cvUrl ?? "" },
        { header: "Cover letter", value: (row) => row.coverLetter ?? "" },
      ],
      rows: applications,
    });
  };

  return (
    <AdminLayout>
      <div className="space-y-6 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("recruit.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {activeCount} {t("recruit.positions")} {t("recruit.positionsOpen")} · {applications.length} {t("recruit.applications")}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button asChild variant="outline">
              <Link to="/admin/recruitment/employment-types">{t("nav.employmentTypes")}</Link>
            </Button>
            <Button asChild>
              <Link to="/admin/recruitment/new">
                <Plus className="mr-1.5 h-4 w-4" /> {t("recruit.postPosition")}
              </Link>
            </Button>
          </div>
        </header>

        {/* ---------- Positions section ---------- */}
        <section className="space-y-3">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between rounded-lg border bg-card p-3">
            <div>
              <h2 className="text-lg font-semibold">{t("recruit.positions")}</h2>
              <p className="text-xs text-muted-foreground">
                {filteredPositions.length} / {positions.length}
              </p>
            </div>
            <div className="w-full sm:w-72">
              <Label className="text-xs" htmlFor="position-search">{t("common.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="position-search"
                  value={positionQuery}
                  onChange={(e) => setPositionQuery(e.target.value)}
                  placeholder={t("recruit.searchPositionPlaceholder")}
                  className="h-9 pl-9"
                />
              </div>
            </div>
          </div>

          {loadingPositions ? (
            <div className="flex justify-center py-10">
              <div className="h-7 w-7 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            </div>
          ) : filteredPositions.length === 0 ? (
            <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
              <div className="rounded-full bg-muted p-3">
                <Briefcase className="h-5 w-5" aria-hidden />
              </div>
              <p>{positions.length === 0 ? t("recruit.noPositions") : t("common.noData")}</p>
              <Button asChild size="sm">
                <Link to="/admin/recruitment/new">
                  <Plus className="mr-1.5 h-4 w-4" /> {t("recruit.postPosition")}
                </Link>
              </Button>
            </div>
          ) : (
            <>
              <BulkActionBar
                selectedCount={posSelectedIds.size}
                bulkDeleting={posBulkDeleting}
                onClear={posClearSelection}
                onBulkDelete={() => void posBulkDelete()}
              />
              <div className="flex items-center gap-2 px-1 text-xs text-muted-foreground">
                <Checkbox
                  checked={
                    posAllSelected
                      ? true
                      : posSomeSelected
                        ? "indeterminate"
                        : false
                  }
                  onCheckedChange={(v) => posToggleAll(v === true)}
                  aria-label={t("common.selectAll")}
                />
                <span>{t("common.selectAll")}</span>
              </div>
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
                {filteredPositions.map((p) => (
                  <div key={p.id} className="relative flex flex-col rounded-lg border bg-card p-5 transition hover:shadow-md">
                    <div
                      className="absolute top-3 right-3 z-10 rounded bg-white/90 p-1 shadow"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <Checkbox
                        checked={posSelectedIds.has(p.id)}
                        onCheckedChange={(v) => posToggleOne(p.id, v === true)}
                        aria-label={`${t("common.selectAll")} · ${p.title}`}
                      />
                    </div>
                    <div className="mb-3 flex items-start justify-between gap-2">
                      <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <Briefcase className="h-5 w-5" strokeWidth={1.75} />
                      </div>
                      <Badge
                        variant="outline"
                        className={cn(
                          "gap-1.5 whitespace-nowrap font-medium",
                          p.isActive
                            ? "border-green-300 bg-green-100 text-green-800"
                            : "border-slate-200 bg-slate-100 text-slate-600",
                        )}
                      >
                        <span
                          className={cn(
                            "h-1.5 w-1.5 rounded-full",
                            p.isActive ? "bg-green-600" : "bg-slate-400",
                          )}
                        />
                        {p.isActive ? t("recruit.hiring") : t("recruit.closed")}
                      </Badge>
                    </div>
                    <h3 className="text-base font-semibold">{p.title}</h3>
                    <p className="text-xs text-muted-foreground">{p.department}</p>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      <Badge variant="outline" className="whitespace-nowrap text-xs">
                        {getEmploymentTypeLabel(p.employmentType, employmentTypeMap.get(p.employmentType) ?? p.employmentType, t)}
                      </Badge>
                      <Badge variant="outline" className="whitespace-nowrap text-xs">
                        {getExperienceLabel(p.experienceLevel, t)}
                      </Badge>
                    </div>
                    <div className="mt-auto flex items-center justify-between pt-4 border-t">
                      <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                        <MapPin className="h-3 w-3" /> {p.location}
                      </span>
                      <span className="flex items-center gap-1 text-xs font-medium text-primary">
                        <Users className="h-3 w-3" /> {p.applicationCount} {t("recruit.candidateCount")}
                      </span>
                    </div>
                    <div className="mt-3 flex gap-2">
                      <Button asChild variant="outline" size="sm" className="flex-1">
                        <Link to={`/admin/recruitment/${p.id}/edit`}>
                          <Pencil className="mr-1 h-3.5 w-3.5" /> {t("recruit.edit")}
                        </Link>
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => deletePosition(p.id, p.title)}
                        disabled={deletingId === p.id}
                        className="flex-1 text-destructive hover:text-destructive"
                      >
                        <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("recruit.delete")}
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </section>

        {/* ---------- Applications section ---------- */}
        <section className="space-y-3">
          <div className="flex flex-col gap-3 rounded-lg border bg-card p-3 sm:flex-row sm:flex-wrap sm:items-end">
            <div className="flex-1">
              <h2 className="text-lg font-semibold">{t("recruit.applications")}</h2>
              <p className="text-xs text-muted-foreground">
                {filteredApplications.length} / {applications.length}
              </p>
            </div>
            <div className="min-w-[200px] flex-1">
              <Label className="text-xs" htmlFor="application-search">{t("common.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="application-search"
                  value={appQuery}
                  onChange={(e) => setAppQuery(e.target.value)}
                  placeholder={t("recruit.searchApplicationPlaceholder")}
                  className="h-9 pl-9"
                />
              </div>
            </div>
            <div className="w-full sm:w-[180px]">
              <Label className="text-xs" htmlFor="filter-position">{t("recruit.position")}</Label>
              <Select
                value={filterPosition === "" ? "__all" : String(filterPosition)}
                onValueChange={(v) => setFilterPosition(v === "__all" ? "" : Number(v))}
              >
                <SelectTrigger id="filter-position" className="h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all">{t("recruit.allPositions")}</SelectItem>
                  {positions.map((p) => (
                    <SelectItem key={p.id} value={String(p.id)}>{p.title}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="w-full sm:w-[160px]">
              <Label className="text-xs" htmlFor="filter-status">{t("common.status")}</Label>
              <Select
                value={filterStatus || "__all"}
                onValueChange={(v) => setFilterStatus(v === "__all" ? "" : v)}
              >
                <SelectTrigger id="filter-status" className="h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all">{t("recruit.allStatuses")}</SelectItem>
                  <SelectItem value="new">{t("recruit.status.new")}</SelectItem>
                  <SelectItem value="interview">{t("recruit.status.interview")}</SelectItem>
                  <SelectItem value="hired">{t("recruit.status.hired")}</SelectItem>
                  <SelectItem value="rejected">{t("recruit.status.rejected")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <AdminExportButton onClick={handleExportApplications} disabled={loadingApps || applications.length === 0} />
            </div>
          </div>

          <div className="space-y-2">
            <BulkActionBar
              selectedCount={appSelectedIds.size}
              bulkDeleting={appBulkDeleting}
              onClear={appClearSelection}
              onBulkDelete={() => void appBulkDelete()}
            />
            {loadingApps ? (
              <div className="flex justify-center py-10">
                <div className="h-7 w-7 animate-spin rounded-full border-4 border-primary border-t-transparent" />
              </div>
            ) : filteredApplications.length === 0 ? (
              <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
                <div className="rounded-full bg-muted p-3">
                  <Users className="h-5 w-5" aria-hidden />
                </div>
                <p>{applications.length === 0 ? t("recruit.noApplications") : t("common.noData")}</p>
              </div>
            ) : (
              <>
                {/* Mobile / tablet card view (<lg) */}
                <ul className="grid gap-3 lg:hidden">
                  {filteredApplications.map((a) => (
                    <li key={a.id} className="rounded-lg border bg-card p-3 shadow-sm">
                      <div className="flex items-start gap-2">
                        <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                          <Checkbox
                            checked={appSelectedIds.has(a.id)}
                            onCheckedChange={(v) => appToggleOne(a.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${a.candidateName}`}
                          />
                        </span>
                        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
                          {a.candidateName[0]?.toUpperCase()}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="break-words text-sm font-semibold leading-tight">{a.candidateName}</p>
                          <p className="mt-0.5 break-all text-xs text-muted-foreground">{a.email}</p>
                          {a.phone && (
                            <p className="text-xs text-muted-foreground">{a.phone}</p>
                          )}
                        </div>
                      </div>

                      <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                        <dt className="text-muted-foreground">{t("recruit.position")}</dt>
                        <dd className="min-w-0 break-words font-medium">{a.positionTitle}</dd>
                        <dt className="text-muted-foreground">{t("recruit.experience")}</dt>
                        <dd>{a.experienceYears != null ? `${a.experienceYears} ${t("recruit.expYears")}` : "—"}</dd>
                        <dt className="text-muted-foreground">{t("recruit.appliedOn")}</dt>
                        <dd>{new Date(a.appliedAt).toLocaleDateString("vi-VN")}</dd>
                      </dl>

                      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t pt-2">
                        <Select value={a.status} onValueChange={(v) => updateAppStatus(a.id, v)}>
                          <SelectTrigger
                            className={cn(
                              "h-7 gap-1.5 rounded-full border px-2.5 text-xs font-medium w-auto min-w-[110px]",
                              APP_STATUS_STYLES[a.status] ?? APP_STATUS_STYLES.new,
                            )}
                          >
                            <span className={cn("h-1.5 w-1.5 rounded-full", APP_STATUS_DOT[a.status] ?? APP_STATUS_DOT.new)} />
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="new">{t("recruit.status.new")}</SelectItem>
                            <SelectItem value="interview">{t("recruit.status.interview")}</SelectItem>
                            <SelectItem value="hired">{t("recruit.status.hired")}</SelectItem>
                            <SelectItem value="rejected">{t("recruit.status.rejected")}</SelectItem>
                          </SelectContent>
                        </Select>
                        <div className="inline-flex items-center gap-1">
                          {a.coverLetter && (
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.view")}
                              aria-label={t("common.view")}
                              onClick={() => alert(a.coverLetter)}
                            >
                              <Eye className="h-4 w-4" />
                            </Button>
                          )}
                          {a.cvUrl && (
                            <Button asChild variant="ghost" size="icon" title="CV" aria-label="CV">
                              <a
                                href={`${import.meta.env.VITE_API_URL ?? ""}${a.cvUrl}`}
                                target="_blank"
                                rel="noopener noreferrer"
                              >
                                <FileDown className="h-4 w-4" />
                              </a>
                            </Button>
                          )}
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => deleteApp(a.id)}
                            title={t("recruit.delete")}
                            aria-label={t("recruit.delete")}
                            className="text-destructive hover:text-destructive"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>

                {/* Desktop table (lg+) */}
                <div className="hidden overflow-x-auto rounded-lg border lg:block">
                <table className="min-w-[900px] w-full divide-y text-sm">
                  <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                    <tr>
                      <th className="w-10 px-3 py-3 text-left">
                        <Checkbox
                          checked={
                            appAllSelected
                              ? true
                              : appSomeSelected
                                ? "indeterminate"
                                : false
                          }
                          onCheckedChange={(v) => appToggleAll(v === true)}
                          aria-label={t("common.selectAll")}
                        />
                      </th>
                      <th className="px-3 py-3 text-left font-medium">{t("recruit.candidate")}</th>
                      <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("recruit.position")}</th>
                      <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("recruit.experience")}</th>
                      <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("recruit.appliedOn")}</th>
                      <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("common.status")}</th>
                      <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {filteredApplications.map((a) => (
                      <tr key={a.id} className="hover:bg-muted/40 transition">
                        <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={appSelectedIds.has(a.id)}
                            onCheckedChange={(v) => appToggleOne(a.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${a.candidateName}`}
                          />
                        </td>
                        <td className="px-3 py-3">
                          <div className="flex items-center gap-3">
                            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
                              {a.candidateName[0]?.toUpperCase()}
                            </div>
                            <div>
                              <p className="font-medium">{a.candidateName}</p>
                              <p className="text-xs text-muted-foreground">{a.email}</p>
                              {a.phone && <p className="text-xs text-muted-foreground">{a.phone}</p>}
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 font-medium">{a.positionTitle}</td>
                        <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">
                          {a.experienceYears != null ? `${a.experienceYears} ${t("recruit.expYears")}` : "—"}
                        </td>
                        <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">
                          {new Date(a.appliedAt).toLocaleDateString("vi-VN")}
                        </td>
                        <td className="whitespace-nowrap px-3 py-3">
                          <Select value={a.status} onValueChange={(v) => updateAppStatus(a.id, v)}>
                            <SelectTrigger
                              className={cn(
                                "h-7 gap-1.5 rounded-full border px-2.5 text-xs font-medium w-auto min-w-[110px]",
                                APP_STATUS_STYLES[a.status] ?? APP_STATUS_STYLES.new,
                              )}
                            >
                              <span className={cn("h-1.5 w-1.5 rounded-full", APP_STATUS_DOT[a.status] ?? APP_STATUS_DOT.new)} />
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="new">{t("recruit.status.new")}</SelectItem>
                              <SelectItem value="interview">{t("recruit.status.interview")}</SelectItem>
                              <SelectItem value="hired">{t("recruit.status.hired")}</SelectItem>
                              <SelectItem value="rejected">{t("recruit.status.rejected")}</SelectItem>
                            </SelectContent>
                          </Select>
                        </td>
                        <td className="whitespace-nowrap px-3 py-3 text-right">
                          <div className="inline-flex items-center gap-1">
                            {a.coverLetter && (
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.view")}
                                aria-label={t("common.view")}
                                onClick={() => alert(a.coverLetter)}
                              >
                                <Eye className="h-4 w-4" />
                              </Button>
                            )}
                            {a.cvUrl && (
                              <Button asChild variant="ghost" size="icon" title="CV" aria-label="CV">
                                <a
                                  href={`${import.meta.env.VITE_API_URL ?? ""}${a.cvUrl}`}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                >
                                  <FileDown className="h-4 w-4" />
                                </a>
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => updateAppStatus(a.id, "interview")}
                              title={t("recruit.status.interview")}
                              aria-label={t("recruit.status.interview")}
                            >
                              <CheckCircle2 className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => updateAppStatus(a.id, "rejected")}
                              title={t("recruit.status.rejected")}
                              aria-label={t("recruit.status.rejected")}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => deleteApp(a.id)}
                              title={t("recruit.delete")}
                              aria-label={t("recruit.delete")}
                              className="text-destructive hover:text-destructive"
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                </div>
              </>
            )}
          </div>
        </section>
      </div>
    </AdminLayout>
  );
};

export default AdminRecruitment;
