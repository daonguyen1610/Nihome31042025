import { useCallback, useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Download,
  FileUp,
  Loader2,
  Plus,
  RefreshCw,
  Search,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type CsvImportError,
  type MaterialRateCatalogResponse,
  type MaterialRateRevisionResponse,
  type MaterialRateRevisionStatus,
  type UpsertMaterialRateCatalogRequest,
} from "@/services/adminApi";

const STATUS_STYLES: Record<MaterialRateRevisionStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Retired: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

const emptyCatalog = (): UpsertMaterialRateCatalogRequest => ({
  code: "",
  name: "",
  description: "",
  currency: "VND",
  isActive: true,
});

const today = () => new Date().toISOString().slice(0, 10);

const AdminMaterialRates = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.materialRatesManage);
  const canApprove = has(ADMIN_PERMS.materialRatesApprove);

  const [catalogs, setCatalogs] = useState<MaterialRateCatalogResponse[]>([]);
  const [selectedCatalogId, setSelectedCatalogId] = useState<number | null>(null);
  const [revisions, setRevisions] = useState<MaterialRateRevisionResponse[]>([]);
  const [selectedRevision, setSelectedRevision] = useState<MaterialRateRevisionResponse | null>(null);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [revisionLoading, setRevisionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedCatalog = useMemo(
    () => catalogs.find((catalog) => catalog.id === selectedCatalogId) ?? null,
    [catalogs, selectedCatalogId],
  );

  const loadCatalogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listMaterialRateCatalogs(search, includeInactive);
      setCatalogs(data);
      setSelectedCatalogId((current) => {
        if (current && data.some((catalog) => catalog.id === current)) return current;
        return data[0]?.id ?? null;
      });
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [includeInactive, search]);

  const loadRevisions = useCallback(async (catalogId: number) => {
    setRevisionLoading(true);
    try {
      const { data } = await adminApi.listMaterialRateRevisions(catalogId);
      setRevisions(data);
      setSelectedRevision((current) =>
        current && data.some((revision) => revision.id === current.id)
          ? data.find((revision) => revision.id === current.id) ?? null
          : data[0] ?? null,
      );
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setRevisionLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    const timer = window.setTimeout(() => void loadCatalogs(), 300);
    return () => window.clearTimeout(timer);
  }, [loadCatalogs]);

  useEffect(() => {
    if (selectedCatalogId) void loadRevisions(selectedCatalogId);
    else {
      setRevisions([]);
      setSelectedRevision(null);
    }
  }, [loadRevisions, selectedCatalogId]);

  const [catalogOpen, setCatalogOpen] = useState(false);
  const [catalogForm, setCatalogForm] = useState<UpsertMaterialRateCatalogRequest>(emptyCatalog());
  const [catalogSaving, setCatalogSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCatalogForm = () => {
    setCatalogForm(emptyCatalog());
    setFormError(null);
    setCatalogOpen(true);
  };

  const saveCatalog = async () => {
    const code = catalogForm.code.trim().toUpperCase();
    const name = catalogForm.name.trim();
    const currency = catalogForm.currency.trim().toUpperCase();
    if (!/^[A-Z0-9][A-Z0-9._-]{0,49}$/.test(code)) {
      setFormError(t("materialRates.validation.catalogCode"));
      return;
    }
    if (!name || name.length > 200 || !/^[A-Z]{3}$/.test(currency)) {
      setFormError(t("materialRates.validation.catalogFields"));
      return;
    }
    if ((catalogForm.description?.trim().length ?? 0) > 1000) {
      setFormError(t("materialRates.validation.description"));
      return;
    }
    setCatalogSaving(true);
    setFormError(null);
    try {
      await adminApi.createMaterialRateCatalog({
        ...catalogForm,
        code,
        name,
        currency,
        description: catalogForm.description?.trim() || null,
      });
      setCatalogOpen(false);
      toast({ title: t("materialRates.catalog.created") });
      await loadCatalogs();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setCatalogSaving(false);
    }
  };

  const [revisionOpen, setRevisionOpen] = useState(false);
  const [effectiveFrom, setEffectiveFrom] = useState(today());
  const [effectiveTo, setEffectiveTo] = useState("");
  const [revisionNote, setRevisionNote] = useState("");
  const [revisionSaving, setRevisionSaving] = useState(false);

  const createRevision = async () => {
    if (!selectedCatalogId || !effectiveFrom) {
      setFormError(t("materialRates.validation.effectiveFrom"));
      return;
    }
    if (effectiveTo && effectiveTo < effectiveFrom) {
      setFormError(t("materialRates.validation.dateRange"));
      return;
    }
    if (revisionNote.trim().length > 1000) {
      setFormError(t("materialRates.validation.note"));
      return;
    }
    setRevisionSaving(true);
    setFormError(null);
    try {
      const { data } = await adminApi.createMaterialRateRevision(selectedCatalogId, {
        effectiveFrom,
        effectiveTo: effectiveTo || null,
        note: revisionNote.trim() || null,
      });
      setRevisionOpen(false);
      setSelectedRevision(data);
      toast({ title: t("materialRates.revision.created") });
      await loadRevisions(selectedCatalogId);
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setRevisionSaving(false);
    }
  };

  const [importErrors, setImportErrors] = useState<CsvImportError[]>([]);
  const [importing, setImporting] = useState(false);
  const importCsv = async (file: File | null) => {
    if (!file || !selectedCatalogId || !selectedRevision) return;
    setImportErrors([]);
    if (!file.name.toLowerCase().endsWith(".csv") || file.size > 2 * 1024 * 1024) {
      setImportErrors([{ message: t("materialRates.validation.csv") }]);
      return;
    }
    setImporting(true);
    try {
      const { data } = await adminApi.importMaterialRateRevision(selectedCatalogId, selectedRevision.id, file);
      toast({ title: t("materialRates.import.done", { count: data.importedCount }) });
      await loadRevisions(selectedCatalogId);
    } catch (err) {
      const responseData = (err as { response?: { data?: { errors?: CsvImportError[] } } }).response?.data;
      if (responseData?.errors?.length) setImportErrors(responseData.errors);
      else setImportErrors([{ message: extractApiError(err) }]);
    } finally {
      setImporting(false);
    }
  };

  const downloadTemplate = async () => {
    try {
      const { data } = await adminApi.downloadMaterialRateTemplate();
      const url = URL.createObjectURL(data);
      const link = document.createElement("a");
      link.href = url;
      link.download = "material-rate-template.csv";
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const [decision, setDecision] = useState<"approve" | "reject" | "retire" | null>(null);
  const [decisionNote, setDecisionNote] = useState("");
  const [decisionBusy, setDecisionBusy] = useState(false);
  const runDecision = async () => {
    if (!decision || !selectedCatalogId || !selectedRevision) return;
    if (decision === "reject" && !decisionNote.trim()) {
      setFormError(t("materialRates.validation.rejectReason"));
      return;
    }
    if (decisionNote.trim().length > 1000) {
      setFormError(t("materialRates.validation.note"));
      return;
    }
    setDecisionBusy(true);
    setFormError(null);
    try {
      if (decision === "approve") {
        await adminApi.approveMaterialRateRevision(selectedCatalogId, selectedRevision.id, decisionNote);
      } else if (decision === "reject") {
        await adminApi.rejectMaterialRateRevision(selectedCatalogId, selectedRevision.id, decisionNote);
      } else {
        await adminApi.retireMaterialRateRevision(selectedCatalogId, selectedRevision.id, decisionNote);
      }
      toast({ title: t(`materialRates.action.${decision}Done`) });
      setDecision(null);
      setDecisionNote("");
      await loadRevisions(selectedCatalogId);
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setDecisionBusy(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-4">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t("materialRates.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("materialRates.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => void downloadTemplate()}>
              <Download className="mr-1.5 h-4 w-4" />{t("materialRates.import.template")}
            </Button>
            {canManage && (
              <Button onClick={openCatalogForm}>
                <Plus className="mr-1.5 h-4 w-4" />{t("materialRates.catalog.new")}
              </Button>
            )}
          </div>
        </header>

        <div className="grid gap-4 lg:grid-cols-[minmax(260px,340px)_minmax(0,1fr)]">
          <section className="space-y-3">
            <div className="rounded-lg border bg-card p-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input value={search} onChange={(event) => setSearch(event.target.value)} className="pl-9" placeholder={t("materialRates.catalog.search")} />
              </div>
              <label className="mt-3 flex items-center gap-2 text-sm text-muted-foreground">
                <Checkbox checked={includeInactive} onCheckedChange={(value) => setIncludeInactive(value === true)} />
                {t("materialRates.catalog.includeInactive")}
              </label>
            </div>
            {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => void loadCatalogs()} /> : catalogs.length === 0 ? (
              <p className="rounded-lg border border-dashed p-6 text-center text-sm text-muted-foreground">{t("materialRates.catalog.empty")}</p>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-1">
                {catalogs.map((catalog) => (
                  <button key={catalog.id} type="button" onClick={() => setSelectedCatalogId(catalog.id)} className={cn("rounded-lg border bg-card p-3 text-left transition-colors hover:border-primary/50", selectedCatalogId === catalog.id && "border-primary bg-primary/5")}>
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0"><p className="truncate font-semibold">{catalog.name}</p><p className="text-xs text-muted-foreground">{catalog.code} · {catalog.currency}</p></div>
                      {!catalog.isActive && <Badge variant="secondary">{t("materialRates.catalog.inactive")}</Badge>}
                    </div>
                    <p className="mt-2 text-xs text-muted-foreground">{t("materialRates.catalog.revisionCount", { count: catalog.revisionCount })}</p>
                  </button>
                ))}
              </div>
            )}
          </section>

          <section className="min-w-0 space-y-3">
            {selectedCatalog ? (
              <>
                <div className="flex flex-col gap-2 rounded-lg border bg-card p-4 sm:flex-row sm:items-start sm:justify-between">
                  <div><h2 className="text-lg font-semibold">{selectedCatalog.name}</h2><p className="text-sm text-muted-foreground">{selectedCatalog.code} · {selectedCatalog.currency}</p>{selectedCatalog.description && <p className="mt-2 text-sm">{selectedCatalog.description}</p>}</div>
                  {canManage && <Button size="sm" onClick={() => { setEffectiveFrom(today()); setEffectiveTo(""); setRevisionNote(""); setFormError(null); setRevisionOpen(true); }}><Plus className="mr-1 h-4 w-4" />{t("materialRates.revision.new")}</Button>}
                </div>

                {revisionLoading ? <PageLoading /> : revisions.length === 0 ? (
                  <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">{t("materialRates.revision.empty")}</p>
                ) : (
                  <div className="grid gap-3 xl:grid-cols-[260px_minmax(0,1fr)]">
                    <div className="space-y-2">
                      {revisions.map((revision) => (
                        <button key={revision.id} type="button" onClick={() => { setSelectedRevision(revision); setImportErrors([]); }} className={cn("w-full rounded-lg border bg-card p-3 text-left hover:border-primary/50", selectedRevision?.id === revision.id && "border-primary bg-primary/5")}>
                          <div className="flex items-center justify-between gap-2"><span className="font-medium">V{revision.version}</span><Badge variant="outline" className={STATUS_STYLES[revision.status]}>{t(`materialRates.status.${revision.status}`)}</Badge></div>
                          <p className="mt-2 text-xs text-muted-foreground">{revision.effectiveFrom} → {revision.effectiveTo || "∞"}</p>
                          <p className="mt-1 text-sm font-semibold">{formatVnd(revision.totalRatePerSqm)} {revision.currency}/m²</p>
                        </button>
                      ))}
                    </div>
                    {selectedRevision && (
                      <div className="min-w-0 space-y-3 rounded-lg border bg-card p-4">
                        <div className="flex flex-wrap items-start justify-between gap-2">
                          <div><h3 className="font-semibold">{t("materialRates.revision.version")} V{selectedRevision.version}</h3><p className="text-sm text-muted-foreground">{selectedRevision.effectiveFrom} → {selectedRevision.effectiveTo || "∞"}</p></div>
                          <div className="text-right"><p className="text-xs text-muted-foreground">{t("materialRates.revision.totalRate")}</p><p className="text-lg font-bold text-primary">{formatVnd(selectedRevision.totalRatePerSqm)} {selectedRevision.currency}/m²</p></div>
                        </div>
                        {selectedRevision.note && <p className="text-sm">{selectedRevision.note}</p>}
                        {selectedRevision.decisionNote && <p className="rounded bg-muted p-2 text-xs">{selectedRevision.decisionNote}</p>}

                        <div className="flex flex-wrap gap-2 border-y py-3">
                          {canManage && selectedRevision.status === "Draft" && <Label className="inline-flex cursor-pointer items-center rounded-md border px-3 py-2 text-sm hover:bg-muted"><FileUp className="mr-1.5 h-4 w-4" />{importing ? t("materialRates.import.importing") : t("materialRates.import.action")}<Input type="file" accept=".csv,text/csv" className="sr-only" disabled={importing} onChange={(event) => void importCsv(event.target.files?.[0] ?? null)} /></Label>}
                          {canApprove && selectedRevision.status === "Draft" && <><Button size="sm" onClick={() => { setDecision("approve"); setDecisionNote(""); setFormError(null); }}><CheckCircle2 className="mr-1 h-4 w-4" />{t("materialRates.action.approve")}</Button><Button size="sm" variant="outline" onClick={() => { setDecision("reject"); setDecisionNote(""); setFormError(null); }}><XCircle className="mr-1 h-4 w-4" />{t("materialRates.action.reject")}</Button></>}
                          {canApprove && selectedRevision.status === "Approved" && <Button size="sm" variant="outline" onClick={() => { setDecision("retire"); setDecisionNote(""); setFormError(null); }}>{t("materialRates.action.retire")}</Button>}
                        </div>

                        {importErrors.length > 0 && <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3"><p className="mb-2 text-sm font-medium text-destructive">{t("materialRates.import.errors")}</p><ul className="space-y-1 text-xs text-destructive">{importErrors.map((item, index) => <li key={`${item.row}-${item.column}-${index}`}>{item.row ? t("materialRates.import.errorLocation", { row: item.row, column: item.column ?? "—" }) : ""} {item.message}</li>)}</ul></div>}

                        {selectedRevision.lines.length === 0 ? <p className="rounded border border-dashed p-6 text-center text-sm text-muted-foreground">{t("materialRates.lines.empty")}</p> : <>
                          <div className="hidden overflow-x-auto md:block"><table className="w-full min-w-[720px] divide-y text-sm"><thead className="bg-muted/40 text-xs text-muted-foreground"><tr><th className="px-2 py-2 text-left">{t("materialRates.field.materialCode")}</th><th className="px-2 py-2 text-left">{t("materialRates.field.materialName")}</th><th className="px-2 py-2 text-left">{t("materialRates.field.unit")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.normPerSqm")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.unitRate")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.wastePercent")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.amountPerSqm")}</th></tr></thead><tbody className="divide-y">{selectedRevision.lines.map((line) => <tr key={line.id}><td className="px-2 py-2">{line.materialCode}</td><td className="px-2 py-2 font-medium">{line.materialName}</td><td className="px-2 py-2">{line.unit}</td><td className="px-2 py-2 text-right">{line.normPerSqm}</td><td className="px-2 py-2 text-right">{formatVnd(line.unitRate)}</td><td className="px-2 py-2 text-right">{line.wastePercent}%</td><td className="px-2 py-2 text-right font-medium">{formatVnd(line.amountPerSqm)}</td></tr>)}</tbody></table></div>
                          <ul className="grid gap-2 md:hidden">{selectedRevision.lines.map((line) => <li key={line.id} className="rounded border p-3 text-sm"><div className="flex justify-between gap-2"><span className="font-medium">{line.materialName}</span><span className="font-semibold">{formatVnd(line.amountPerSqm)}</span></div><p className="text-xs text-muted-foreground">{line.materialCode} · {line.normPerSqm} {line.unit}/m² · {formatVnd(line.unitRate)} · {line.wastePercent}%</p></li>)}</ul>
                        </>}
                      </div>
                    )}
                  </div>
                )}
              </>
            ) : !loading && <p className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">{t("materialRates.catalog.select")}</p>}
          </section>
        </div>
      </div>

      <Dialog open={catalogOpen} onOpenChange={setCatalogOpen}><DialogContent><DialogHeader><DialogTitle>{t("materialRates.catalog.new")}</DialogTitle><DialogDescription>{t("materialRates.catalog.formHint")}</DialogDescription></DialogHeader><div className="space-y-3"><div><Label>{t("materialRates.catalog.code")} *</Label><Input maxLength={50} value={catalogForm.code} onChange={(event) => setCatalogForm({ ...catalogForm, code: event.target.value })} /></div><div><Label>{t("materialRates.catalog.name")} *</Label><Input maxLength={200} value={catalogForm.name} onChange={(event) => setCatalogForm({ ...catalogForm, name: event.target.value })} /></div><div><Label>{t("materialRates.catalog.currency")} *</Label><Input maxLength={3} value={catalogForm.currency} onChange={(event) => setCatalogForm({ ...catalogForm, currency: event.target.value })} /></div><div><Label>{t("materialRates.catalog.description")}</Label><Textarea maxLength={1000} value={catalogForm.description ?? ""} onChange={(event) => setCatalogForm({ ...catalogForm, description: event.target.value })} /></div>{formError && <p className="text-sm text-destructive">{formError}</p>}</div><DialogFooter><Button variant="outline" onClick={() => setCatalogOpen(false)}>{t("common.cancel")}</Button><Button onClick={() => void saveCatalog()} disabled={catalogSaving}>{catalogSaving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={revisionOpen} onOpenChange={setRevisionOpen}><DialogContent><DialogHeader><DialogTitle>{t("materialRates.revision.new")}</DialogTitle><DialogDescription>{selectedCatalog?.name}</DialogDescription></DialogHeader><div className="space-y-3"><div className="grid grid-cols-2 gap-3"><div><Label>{t("materialRates.revision.effectiveFrom")} *</Label><Input type="date" value={effectiveFrom} onChange={(event) => setEffectiveFrom(event.target.value)} /></div><div><Label>{t("materialRates.revision.effectiveTo")}</Label><Input type="date" value={effectiveTo} min={effectiveFrom} onChange={(event) => setEffectiveTo(event.target.value)} /></div></div><div><Label>{t("materialRates.revision.note")}</Label><Textarea maxLength={1000} value={revisionNote} onChange={(event) => setRevisionNote(event.target.value)} /></div>{formError && <p className="text-sm text-destructive">{formError}</p>}</div><DialogFooter><Button variant="outline" onClick={() => setRevisionOpen(false)}>{t("common.cancel")}</Button><Button onClick={() => void createRevision()} disabled={revisionSaving}>{revisionSaving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}><DialogContent><DialogHeader><DialogTitle>{decision && t(`materialRates.action.${decision}`)}</DialogTitle><DialogDescription>{t("materialRates.action.decisionHint")}</DialogDescription></DialogHeader><div><Label>{t("materialRates.revision.decisionNote")}{decision === "reject" ? " *" : ""}</Label><Textarea maxLength={1000} value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} /></div>{formError && <p className="text-sm text-destructive">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setDecision(null)}>{t("common.cancel")}</Button><Button onClick={() => void runDecision()} disabled={decisionBusy}>{decisionBusy && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.confirm")}</Button></DialogFooter></DialogContent></Dialog>
    </AdminLayout>
  );
};

export default AdminMaterialRates;
