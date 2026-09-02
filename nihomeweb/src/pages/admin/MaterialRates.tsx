import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowRight,
  Calculator,
  CheckCircle2,
  Download,
  FileCheck2,
  FileSpreadsheet,
  Info,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
  Trash2,
  UploadCloud,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
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
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import { formatVnd } from "@/lib/numberFormat";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type CsvImportError,
  type MaterialRateCatalogType,
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

const emptyCatalog = (catalogType: MaterialRateCatalogType): UpsertMaterialRateCatalogRequest => ({
  catalogType,
  code: "",
  name: "",
  description: "",
  currency: "VND",
  isActive: true,
});

const today = () => new Date().toISOString().slice(0, 10);
const formatFileSize = (bytes: number) => bytes < 1024 * 1024
  ? `${Math.max(1, Math.round(bytes / 1024))} KB`
  : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

const IMPORT_FIELD_KEYS: Record<string, string> = {
  MaterialCode: "materialRates.field.materialCode",
  ItemCode: "materialRates.field.itemCode",
  MaterialName: "materialRates.field.materialName",
  ItemName: "materialRates.field.itemName",
  Unit: "materialRates.field.unit",
  Quantity: "materialRates.field.quantity",
  NormPerSqm: "materialRates.field.normPerSqm",
  UnitRate: "materialRates.field.unitRate",
  UnitPrice: "materialRates.field.unitRate",
  WastePercent: "materialRates.field.wastePercent",
  AmountPerSqm: "materialRates.field.amountPerSqm",
  TotalAmount: "materialRates.field.totalAmount",
};

interface AdminMaterialRatesProps {
  catalogType?: MaterialRateCatalogType;
}

const AdminMaterialRates = ({ catalogType = "InvestmentRate" }: AdminMaterialRatesProps) => {
  const { lang, t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.materialRatesManage);
  const canApprove = has(ADMIN_PERMS.materialRatesApprove);
  const canViewQuotes = has(ADMIN_PERMS.quotes);

  const [catalogs, setCatalogs] = useState<MaterialRateCatalogResponse[]>([]);
  const [selectedCatalogId, setSelectedCatalogId] = useState<number | null>(null);
  const [revisions, setRevisions] = useState<MaterialRateRevisionResponse[]>([]);
  const [selectedRevision, setSelectedRevision] = useState<MaterialRateRevisionResponse | null>(null);
  const [search, setSearch] = useState("");
  const [includeInactive, setIncludeInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [revisionLoading, setRevisionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bulkDeleteFailures, setBulkDeleteFailures] = useState<Array<{ id: number; name: string; message: string }>>([]);

  const apiErrorMessage = useCallback((err: unknown) => {
    const responseData = (err as { response?: { data?: { messageKey?: string } } }).response?.data;
    return responseData?.messageKey ? t(responseData.messageKey) : extractApiError(err);
  }, [t]);

  const selectedCatalog = useMemo(
    () => catalogs.find((catalog) => catalog.id === selectedCatalogId) ?? null,
    [catalogs, selectedCatalogId],
  );
  const visibleCatalogIds = useMemo(() => catalogs.map((catalog) => catalog.id), [catalogs]);

  const loadCatalogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listMaterialRateCatalogs(search, includeInactive, catalogType);
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
  }, [catalogType, includeInactive, search]);

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

  const deleteCatalogRequest = useCallback(
    (id: number) => adminApi.deleteMaterialRateCatalog(id),
    [],
  );
  const {
    selectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  } = useBulkSelection({
    visibleIds: visibleCatalogIds,
    deleteOne: deleteCatalogRequest,
    confirmMessage: t("materialRates.catalog.deleteManyConfirm"),
    onAfter: async ({ failures }) => {
      setBulkDeleteFailures(failures.map(({ id, reason }) => ({
        id,
        name: catalogs.find((catalog) => catalog.id === id)?.name ?? String(id),
        message: apiErrorMessage(reason),
      })));
      await loadCatalogs();
    },
  });

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

  useEffect(() => {
    clearSelection();
    setBulkDeleteFailures([]);
  }, [catalogType, clearSelection, includeInactive, search]);

  const [catalogOpen, setCatalogOpen] = useState(false);
  const [catalogForm, setCatalogForm] = useState<UpsertMaterialRateCatalogRequest>(emptyCatalog(catalogType));
  const [editingCatalogId, setEditingCatalogId] = useState<number | null>(null);
  const [catalogSaving, setCatalogSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCatalogForm = () => {
    setEditingCatalogId(null);
    setCatalogForm(emptyCatalog(catalogType));
    setFormError(null);
    setCatalogOpen(true);
  };

  const openCatalogEdit = () => {
    if (!selectedCatalog) return;
    setEditingCatalogId(selectedCatalog.id);
    setCatalogForm({
      catalogType,
      code: selectedCatalog.code,
      name: selectedCatalog.name,
      description: selectedCatalog.description,
      currency: selectedCatalog.currency,
      isActive: selectedCatalog.isActive,
    });
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
      const request = {
        ...catalogForm,
        code,
        name,
        currency,
        description: catalogForm.description?.trim() || null,
      };
      if (editingCatalogId) await adminApi.updateMaterialRateCatalog(editingCatalogId, request);
      else await adminApi.createMaterialRateCatalog(request);
      setCatalogOpen(false);
      toast({ title: t(editingCatalogId ? "materialRates.catalog.updated" : "materialRates.catalog.created") });
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
  const [pendingImportFile, setPendingImportFile] = useState<File | null>(null);
  const [importedCount, setImportedCount] = useState<number | null>(null);
  const [importConfirmOpen, setImportConfirmOpen] = useState(false);
  const selectImportFile = (file: File | null) => {
    if (!file || !selectedCatalogId || !selectedRevision) return;
    setImportErrors([]);
    setImportedCount(null);
    const extension = file.name.toLowerCase().split(".").pop();
    if (!extension || !["xlsx", "csv"].includes(extension) || file.size > 5 * 1024 * 1024) {
      setImportErrors([{ message: t("materialRates.validation.csv") }]);
      return;
    }
    if (file.size === 0) {
      setImportErrors([{ message: t("materialRates.validation.csvEmpty") }]);
      return;
    }
    setPendingImportFile(file);
  };

  const importCsv = async () => {
    if (!pendingImportFile || !selectedCatalogId || !selectedRevision) return;
    setImportConfirmOpen(false);
    setImporting(true);
    try {
      const { data } = await adminApi.importMaterialRateRevision(
        selectedCatalogId,
        selectedRevision.id,
        pendingImportFile,
      );
      setImportedCount(data.importedCount);
      setPendingImportFile(null);
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

  const downloadTemplatePackage = async () => {
    try {
      const { data } = await adminApi.downloadMaterialRateExcelTemplate(lang, catalogType);
      const url = URL.createObjectURL(data);
      const link = document.createElement("a");
      link.href = url;
      link.download = t(catalogType === "Boq" ? "materialRates.excel.boqFileName" : "materialRates.excel.fileName");
      link.click();
      URL.revokeObjectURL(url);
      toast({ title: t("materialRates.package.downloaded") });
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const [deleteCatalogOpen, setDeleteCatalogOpen] = useState(false);
  const [deleteCatalogBusy, setDeleteCatalogBusy] = useState(false);
  const [deleteCatalogError, setDeleteCatalogError] = useState<string | null>(null);
  const deleteSelectedCatalog = async () => {
    if (!selectedCatalog) return;
    setDeleteCatalogBusy(true);
    setDeleteCatalogError(null);
    try {
      await deleteCatalogRequest(selectedCatalog.id);
      setDeleteCatalogOpen(false);
      setSelectedCatalogId(null);
      setRevisions([]);
      setSelectedRevision(null);
      toast({ title: t("materialRates.catalog.deleted") });
      await loadCatalogs();
    } catch (err) {
      setDeleteCatalogError(apiErrorMessage(err));
    } finally {
      setDeleteCatalogBusy(false);
    }
  };

  const [decision, setDecision] = useState<"approve" | "reject" | null>(null);
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
      } else {
        await adminApi.rejectMaterialRateRevision(selectedCatalogId, selectedRevision.id, decisionNote);
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

  const completedSteps = [
    Boolean(selectedCatalog),
    Boolean(selectedRevision),
    Boolean(selectedRevision?.lines.length),
    selectedRevision?.status === "Approved" || selectedRevision?.status === "Retired",
  ];
  const currentStep = completedSteps.findIndex((complete) => !complete);
  const isTerminalRevision = selectedRevision?.status === "Rejected" || selectedRevision?.status === "Retired";
  const workflowSteps = [
    { icon: FileSpreadsheet, title: t("materialRates.workflow.catalog"), detail: t("materialRates.workflow.catalogHint") },
    { icon: RefreshCw, title: t("materialRates.workflow.revision"), detail: t("materialRates.workflow.revisionHint") },
    { icon: UploadCloud, title: t("materialRates.workflow.import"), detail: t("materialRates.workflow.importHint") },
    { icon: ShieldCheck, title: t("materialRates.workflow.approve"), detail: t(catalogType === "Boq" ? "materialRates.boq.workflow.approveHint" : "materialRates.workflow.approveHint") },
  ];
  const importErrorMessage = (item: CsvImportError) => {
    if (!item.messageKey) return item.message;
    const args = { ...(item.messageArgs ?? {}) };
    if (typeof args.field === "string" && IMPORT_FIELD_KEYS[args.field]) {
      args.field = t(IMPORT_FIELD_KEYS[args.field]);
    }
    return t(item.messageKey, args);
  };

  return (
    <AdminLayout>
      <div className="space-y-4" data-testid="material-rates-page">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{t(catalogType === "Boq" ? "materialRates.boq.title" : "materialRates.title")}</h1>
            <p className="text-sm text-muted-foreground">{t(catalogType === "Boq" ? "materialRates.boq.subtitle" : "materialRates.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" data-testid="material-rates-download-package" onClick={() => void downloadTemplatePackage()}>
              <Download className="mr-1.5 h-4 w-4" />{t("materialRates.package.download")}
            </Button>
            {canManage && (
              <Button onClick={openCatalogForm}>
                <Plus className="mr-1.5 h-4 w-4" />{t("materialRates.catalog.new")}
              </Button>
            )}
          </div>
        </header>

        <nav className="flex w-fit rounded-lg border bg-muted/30 p-1" aria-label={t("materialRates.catalogType.navigation")}>
          <Button variant={catalogType === "InvestmentRate" ? "secondary" : "ghost"} size="sm" asChild><Link to="/admin/material-rates/investment">{t("materialRates.catalogType.investment")}</Link></Button>
          <Button variant={catalogType === "Boq" ? "secondary" : "ghost"} size="sm" asChild><Link to="/admin/material-rates/boq">{t("materialRates.catalogType.boq")}</Link></Button>
        </nav>

        <section className="overflow-hidden rounded-xl border bg-card shadow-sm">
          <div className="border-b bg-gradient-to-r from-primary/10 via-primary/5 to-transparent px-4 py-4 sm:px-5">
            <div className="flex items-start gap-3">
              <div className="rounded-lg bg-primary p-2 text-primary-foreground"><Calculator className="h-5 w-5" /></div>
              <div>
                <h2 className="font-semibold">{t("materialRates.workflow.title")}</h2>
                <p className="mt-0.5 text-sm text-muted-foreground">{t("materialRates.workflow.subtitle")}</p>
              </div>
            </div>
          </div>
          <ol className="grid divide-y sm:grid-cols-2 sm:divide-x sm:divide-y-0 xl:grid-cols-4">
            {workflowSteps.map((step, index) => {
              const complete = completedSteps[index];
              const active = !isTerminalRevision && (currentStep === index || (currentStep === -1 && index === workflowSteps.length - 1));
              const StepIcon = step.icon;
              return (
                <li key={step.title} data-testid={`material-rates-workflow-step-${index + 1}`} data-state={complete ? "complete" : active ? "active" : "pending"} className={cn("flex gap-3 p-4", active && "bg-primary/[0.04]")}>
                  <div className={cn("flex h-8 w-8 shrink-0 items-center justify-center rounded-full border text-sm font-semibold", complete ? "border-emerald-500 bg-emerald-500 text-white" : active ? "border-primary bg-primary text-primary-foreground" : "bg-muted text-muted-foreground")}>
                    {complete ? <CheckCircle2 className="h-4 w-4" /> : index + 1}
                  </div>
                  <div className="min-w-0">
                    <p className="flex items-center gap-1.5 text-sm font-semibold"><StepIcon className="h-4 w-4 text-muted-foreground" />{step.title}</p>
                    <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{step.detail}</p>
                  </div>
                </li>
              );
            })}
          </ol>
        </section>

        <div className="grid gap-4 lg:grid-cols-[minmax(260px,340px)_minmax(0,1fr)]">
          <section className="space-y-3">
            <div className="rounded-lg border bg-card p-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input value={search} onChange={(event) => setSearch(event.target.value)} className="pl-9" placeholder={t("materialRates.catalog.search")} />
              </div>
              <label className="mt-3 flex items-center gap-2 text-sm text-muted-foreground">
                <Checkbox data-testid="material-rates-include-inactive" checked={includeInactive} onCheckedChange={(value) => setIncludeInactive(value === true)} />
                {t("materialRates.catalog.includeInactive")}
              </label>
              {canManage && catalogs.length > 0 && (
                <label className="mt-3 flex items-center gap-2 border-t pt-3 text-sm text-muted-foreground">
                  <Checkbox
                    data-testid="material-rates-select-all"
                    aria-label={t("common.selectAll")}
                    checked={allVisibleSelected ? true : someVisibleSelected ? "indeterminate" : false}
                    onCheckedChange={(value) => toggleAllVisible(value === true)}
                  />
                  {allVisibleSelected ? t("common.deselectAll") : t("common.selectAll")}
                </label>
              )}
            </div>
            {canManage && (
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={() => { clearSelection(); setBulkDeleteFailures([]); }}
                onBulkDelete={() => { setBulkDeleteFailures([]); void handleBulkDelete(); }}
              />
            )}
            {bulkDeleteFailures.length > 0 && (
              <div data-testid="material-rates-bulk-delete-errors" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive" role="alert">
                <p className="font-medium">{t("materialRates.catalog.bulkDeleteFailed")}</p>
                <ul className="mt-2 space-y-1">
                  {bulkDeleteFailures.map((failure) => <li key={failure.id}><span className="font-medium">{failure.name}:</span> {failure.message}</li>)}
                </ul>
              </div>
            )}
            {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => void loadCatalogs()} /> : catalogs.length === 0 ? (
              <div className="rounded-lg border border-dashed px-4 py-10 text-center">
                <FileSpreadsheet className="mx-auto h-9 w-9 text-muted-foreground/60" />
                <p className="mt-3 text-sm font-medium">{t("materialRates.catalog.empty")}</p>
                <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{t("materialRates.catalog.emptyHint")}</p>
                {canManage && <Button size="sm" className="mt-4" onClick={openCatalogForm}><Plus className="mr-1 h-4 w-4" />{t("materialRates.catalog.new")}</Button>}
              </div>
            ) : (
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-1">
                {catalogs.map((catalog) => (
                  <div key={catalog.id} className={cn("flex items-start gap-3 rounded-lg border bg-card p-3 transition-colors hover:border-primary/50", selectedCatalogId === catalog.id && "border-primary bg-primary/5")}>
                    {canManage && (
                      <Checkbox
                        className="mt-0.5 shrink-0"
                        data-testid={`material-rates-select-${catalog.id}`}
                        aria-label={`${t("common.selectAll")} · ${catalog.name}`}
                        checked={selectedIds.has(catalog.id)}
                        onCheckedChange={(value) => toggleOne(catalog.id, value === true)}
                      />
                    )}
                    <button type="button" onClick={() => setSelectedCatalogId(catalog.id)} className="min-w-0 flex-1 text-left">
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0"><p className="truncate font-semibold">{catalog.name}</p><p className="text-xs text-muted-foreground">{catalog.code} · {catalog.currency}</p></div>
                        {!catalog.isActive && <Badge variant="secondary">{t("materialRates.catalog.inactive")}</Badge>}
                      </div>
                      <p className="mt-2 text-xs text-muted-foreground">{t("materialRates.catalog.revisionCount", { count: catalog.revisionCount })}</p>
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="min-w-0 space-y-3">
            {selectedCatalog ? (
              <>
                <div className="flex flex-col gap-2 rounded-lg border bg-card p-4 sm:flex-row sm:items-start sm:justify-between">
                  <div><h2 className="text-lg font-semibold">{selectedCatalog.name}</h2><p className="text-sm text-muted-foreground">{selectedCatalog.code} · {selectedCatalog.currency}</p>{selectedCatalog.description && <p className="mt-2 text-sm">{selectedCatalog.description}</p>}</div>
                  {canManage && <div className="flex flex-wrap gap-2"><Button size="sm" variant="outline" data-testid="material-rates-edit-catalog" onClick={openCatalogEdit}><Pencil className="mr-1 h-4 w-4" />{t("common.edit")}</Button><Button size="sm" variant="destructive" data-testid="material-rates-delete-catalog" onClick={() => { setDeleteCatalogError(null); setDeleteCatalogOpen(true); }}><Trash2 className="mr-1 h-4 w-4" />{t("materialRates.catalog.delete")}</Button><Button size="sm" data-testid="material-rates-new-revision" onClick={() => { setEffectiveFrom(today()); setEffectiveTo(""); setRevisionNote(""); setFormError(null); setRevisionOpen(true); }}><Plus className="mr-1 h-4 w-4" />{t("materialRates.revision.new")}</Button></div>}
                </div>

                {revisionLoading ? <PageLoading /> : revisions.length === 0 ? (
                  <div className="rounded-lg border border-dashed px-4 py-10 text-center">
                    <RefreshCw className="mx-auto h-9 w-9 text-muted-foreground/60" />
                    <p className="mt-3 text-sm font-medium">{t("materialRates.revision.empty")}</p>
                    <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{t("materialRates.revision.emptyHint")}</p>
                    {canManage && <Button size="sm" className="mt-4" onClick={() => { setEffectiveFrom(today()); setEffectiveTo(""); setRevisionNote(""); setFormError(null); setRevisionOpen(true); }}><Plus className="mr-1 h-4 w-4" />{t("materialRates.revision.new")}</Button>}
                  </div>
                ) : (
                  <div className="grid gap-3 xl:grid-cols-[260px_minmax(0,1fr)]">
                    <div className="space-y-2">
                      {revisions.map((revision) => (
                        <button key={revision.id} type="button" onClick={() => { setSelectedRevision(revision); setImportErrors([]); setPendingImportFile(null); setImportedCount(null); }} className={cn("w-full rounded-lg border bg-card p-3 text-left hover:border-primary/50", selectedRevision?.id === revision.id && "border-primary bg-primary/5")}>
                          <div className="flex items-center justify-between gap-2"><span className="font-medium">V{revision.version}</span><Badge variant="outline" className={STATUS_STYLES[revision.status]}>{t(`materialRates.status.${revision.status}`)}</Badge></div>
                          <p className="mt-2 text-xs text-muted-foreground">{revision.effectiveFrom} → {revision.effectiveTo || "∞"}</p>
                          <p className="mt-1 text-sm font-semibold">{formatVnd(catalogType === "Boq" ? revision.totalAmount : revision.totalRatePerSqm)} {revision.currency}{catalogType === "InvestmentRate" ? "/m²" : ""}</p>
                        </button>
                      ))}
                    </div>
                    {selectedRevision && (
                      <div className="min-w-0 space-y-3 rounded-lg border bg-card p-4">
                        <div className="flex flex-wrap items-start justify-between gap-2">
                          <div><h3 className="font-semibold">{t("materialRates.revision.version")} V{selectedRevision.version}</h3><p className="text-sm text-muted-foreground">{selectedRevision.effectiveFrom} → {selectedRevision.effectiveTo || "∞"}</p></div>
                          <div className="text-right"><p className="text-xs text-muted-foreground">{t(catalogType === "Boq" ? "materialRates.revision.totalAmount" : "materialRates.revision.totalRate")}</p><p className="text-lg font-bold text-primary">{formatVnd(catalogType === "Boq" ? selectedRevision.totalAmount : selectedRevision.totalRatePerSqm)} {selectedRevision.currency}{catalogType === "InvestmentRate" ? "/m²" : ""}</p></div>
                        </div>
                        {selectedRevision.note && <p className="text-sm">{selectedRevision.note}</p>}
                        {selectedRevision.decisionNote && <p className="rounded bg-muted p-2 text-xs">{selectedRevision.decisionNote}</p>}
                        {isTerminalRevision && <div className="flex gap-2 rounded-md border bg-muted/40 p-3 text-sm" data-testid="material-rates-terminal-hint"><Info className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" /><span>{t("materialRates.revision.terminalHint")}</span></div>}

                        {canManage && selectedRevision.status === "Draft" && (
                          <section className="rounded-lg border bg-muted/20 p-4">
                            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                              <div>
                                <h4 className="flex items-center gap-2 text-sm font-semibold"><FileSpreadsheet className="h-4 w-4 text-primary" />{t("materialRates.import.title")}</h4>
                                <p className="mt-1 max-w-2xl text-xs leading-relaxed text-muted-foreground">{t("materialRates.import.packageHint")}</p>
                              </div>
                              <Button size="sm" variant="outline" onClick={() => void downloadTemplatePackage()}><Download className="mr-1.5 h-4 w-4" />{t("materialRates.package.download")}</Button>
                            </div>
                            <Label
                              className="mt-4 flex min-h-32 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed bg-background px-4 py-6 text-center transition-colors hover:border-primary/60 hover:bg-primary/[0.02]"
                              onDragOver={(event) => event.preventDefault()}
                              onDrop={(event) => { event.preventDefault(); selectImportFile(event.dataTransfer.files?.[0] ?? null); }}
                            >
                              <UploadCloud className="h-8 w-8 text-primary" />
                              <span className="mt-2 text-sm font-medium">{t("materialRates.import.select")}</span>
                              <span className="mt-1 text-xs text-muted-foreground">{t("materialRates.import.requirements")}</span>
                              <Input type="file" accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv" className="sr-only" data-testid="material-rates-import-file" disabled={importing} onChange={(event) => { selectImportFile(event.target.files?.[0] ?? null); event.target.value = ""; }} />
                            </Label>
                            {pendingImportFile && (
                              <div className="mt-3 flex flex-col gap-3 rounded-md border bg-background p-3 sm:flex-row sm:items-center sm:justify-between" data-testid="material-rates-selected-file">
                                <div className="flex min-w-0 flex-1 items-center gap-2"><FileCheck2 className="h-5 w-5 shrink-0 text-emerald-600" /><div className="min-w-0 flex-1"><p className="break-all text-sm font-medium">{pendingImportFile.name}</p><p className="text-xs text-muted-foreground">{formatFileSize(pendingImportFile.size)}</p></div></div>
                                <Button size="sm" data-testid="material-rates-import-review" onClick={() => setImportConfirmOpen(true)} disabled={importing}><UploadCloud className="mr-1.5 h-4 w-4" />{t("materialRates.import.confirmAction")}</Button>
                              </div>
                            )}
                            <div className="mt-3 flex gap-2 rounded-md border border-amber-300/70 bg-amber-50 p-3 text-xs leading-relaxed text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100"><Info className="mt-0.5 h-4 w-4 shrink-0" /><span>{t("materialRates.import.replaceWarning")}</span></div>
                          </section>
                        )}

                        {importedCount !== null && (
                          <div className="flex flex-col gap-3 rounded-md border border-emerald-300 bg-emerald-50 p-3 text-emerald-950 dark:border-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-100 sm:flex-row sm:items-center sm:justify-between">
                            <div className="flex gap-2"><CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0" /><div><p className="text-sm font-semibold">{t("materialRates.import.result", { count: importedCount })}</p><p className="text-xs">{t(catalogType === "Boq" ? "materialRates.boq.import.nextStep" : "materialRates.import.nextStep")}</p></div></div>
                            <p className="whitespace-nowrap text-sm font-bold">{formatVnd(catalogType === "Boq" ? selectedRevision.totalAmount : selectedRevision.totalRatePerSqm)} {selectedRevision.currency}{catalogType === "InvestmentRate" ? "/m²" : ""}</p>
                          </div>
                        )}

                        <div className="flex flex-wrap items-center gap-2 border-y py-3">
                          {canApprove && selectedRevision.status === "Draft" && <><Button size="sm" data-testid="material-rates-approve" disabled={selectedRevision.lines.length === 0} title={selectedRevision.lines.length === 0 ? t("materialRates.action.approveDisabled") : undefined} onClick={() => { setDecision("approve"); setDecisionNote(""); setFormError(null); }}><CheckCircle2 className="mr-1 h-4 w-4" />{t("materialRates.action.approve")}</Button><Button size="sm" variant="outline" onClick={() => { setDecision("reject"); setDecisionNote(""); setFormError(null); }}><XCircle className="mr-1 h-4 w-4" />{t("materialRates.action.reject")}</Button></>}
                          {selectedRevision.status === "Approved" && selectedCatalog.isActive && canViewQuotes && <Button size="sm" asChild><Link to="/admin/quotes" data-testid="material-rates-open-quotes">{t("materialRates.quote.open")}<ArrowRight className="ml-1.5 h-4 w-4" /></Link></Button>}
                          {selectedRevision.status === "Approved" && <p className="basis-full text-xs text-muted-foreground">{selectedCatalog.isActive ? t(catalogType === "Boq" ? "materialRates.boq.quote.hint" : "materialRates.quote.hint") : t("materialRates.quote.inactiveHint")}</p>}
                        </div>

                        {importErrors.length > 0 && <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3"><p className="mb-2 text-sm font-medium text-destructive">{t("materialRates.import.errors")}</p><ul className="space-y-1 text-xs text-destructive">{importErrors.map((item, index) => <li key={`${item.row}-${item.column}-${index}`}>{item.row ? t("materialRates.import.errorLocation", { row: item.row, column: item.column ?? "—" }) : ""} {importErrorMessage(item)}</li>)}</ul></div>}

                        {selectedRevision.lines.length === 0 ? <p className="rounded border border-dashed p-6 text-center text-sm text-muted-foreground">{t("materialRates.lines.empty")}</p> : <>
                          <div className="hidden overflow-x-auto md:block"><table className="w-full min-w-[720px] divide-y text-sm"><thead className="bg-muted/40 text-xs text-muted-foreground"><tr><th className="px-2 py-2 text-left">{t(catalogType === "Boq" ? "materialRates.field.itemCode" : "materialRates.field.materialCode")}</th><th className="px-2 py-2 text-left">{t(catalogType === "Boq" ? "materialRates.field.itemName" : "materialRates.field.materialName")}</th><th className="px-2 py-2 text-left">{t("materialRates.field.unit")}</th>{catalogType === "Boq" ? <><th className="px-2 py-2 text-right">{t("materialRates.field.quantity")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.unitRate")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.totalAmount")}</th></> : <><th className="px-2 py-2 text-right">{t("materialRates.field.normPerSqm")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.unitRate")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.wastePercent")}</th><th className="px-2 py-2 text-right">{t("materialRates.field.amountPerSqm")}</th></>}</tr></thead><tbody className="divide-y">{selectedRevision.lines.map((line) => <tr key={line.id}><td className="px-2 py-2">{line.materialCode}</td><td className="px-2 py-2 font-medium">{line.materialName}</td><td className="px-2 py-2">{line.unit}</td>{catalogType === "Boq" ? <><td className="px-2 py-2 text-right">{line.quantity}</td><td className="px-2 py-2 text-right">{formatVnd(line.unitRate)}</td><td className="px-2 py-2 text-right font-medium">{formatVnd(line.quantity * line.unitRate)}</td></> : <><td className="px-2 py-2 text-right">{line.normPerSqm}</td><td className="px-2 py-2 text-right">{formatVnd(line.unitRate)}</td><td className="px-2 py-2 text-right">{line.wastePercent}%</td><td className="px-2 py-2 text-right font-medium">{formatVnd(line.amountPerSqm)}</td></>}</tr>)}</tbody></table></div>
                          <ul className="grid gap-2 md:hidden">{selectedRevision.lines.map((line) => <li key={line.id} className="rounded border p-3 text-sm"><div className="flex justify-between gap-2"><span className="font-medium">{line.materialName}</span><span className="font-semibold">{formatVnd(catalogType === "Boq" ? line.quantity * line.unitRate : line.amountPerSqm)}</span></div><p className="text-xs text-muted-foreground">{catalogType === "Boq" ? `${line.materialCode} · ${line.quantity} ${line.unit} · ${formatVnd(line.unitRate)}` : `${line.materialCode} · ${line.normPerSqm} ${line.unit}/m² · ${formatVnd(line.unitRate)} · ${line.wastePercent}%`}</p></li>)}</ul>
                        </>}
                      </div>
                    )}
                  </div>
                )}
              </>
            ) : !loading && <div data-testid="material-rates-empty-detail" className="flex min-h-60 items-center justify-center rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">{t("materialRates.catalog.select")}</div>}
          </section>
        </div>
      </div>

      <Dialog open={catalogOpen} onOpenChange={setCatalogOpen}><DialogContent><DialogHeader><DialogTitle>{t(editingCatalogId ? "materialRates.catalog.edit" : "materialRates.catalog.new")}</DialogTitle><DialogDescription>{t("materialRates.catalog.formHint")}</DialogDescription></DialogHeader><div className="space-y-3"><div><Label>{t("materialRates.catalog.code")} *</Label><Input data-testid="material-rates-catalog-code" maxLength={50} value={catalogForm.code} onChange={(event) => setCatalogForm({ ...catalogForm, code: event.target.value })} /></div><div><Label>{t("materialRates.catalog.name")} *</Label><Input data-testid="material-rates-catalog-name" maxLength={200} value={catalogForm.name} onChange={(event) => setCatalogForm({ ...catalogForm, name: event.target.value })} /></div><div><Label>{t("materialRates.catalog.currency")} *</Label><Input data-testid="material-rates-catalog-currency" maxLength={3} value={catalogForm.currency} onChange={(event) => setCatalogForm({ ...catalogForm, currency: event.target.value })} /></div><div><Label>{t("materialRates.catalog.description")}</Label><Textarea data-testid="material-rates-catalog-description" maxLength={1000} value={catalogForm.description ?? ""} onChange={(event) => setCatalogForm({ ...catalogForm, description: event.target.value })} /></div><Label className="flex items-center gap-2"><Checkbox data-testid="material-rates-catalog-active" checked={catalogForm.isActive} onCheckedChange={(value) => setCatalogForm({ ...catalogForm, isActive: value === true })} />{t("materialRates.catalog.active")}</Label>{formError && <p className="text-sm text-destructive">{formError}</p>}</div><DialogFooter><Button variant="outline" onClick={() => setCatalogOpen(false)}>{t("common.cancel")}</Button><Button data-testid="material-rates-catalog-save" onClick={() => void saveCatalog()} disabled={catalogSaving}>{catalogSaving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={revisionOpen} onOpenChange={setRevisionOpen}><DialogContent><DialogHeader><DialogTitle>{t("materialRates.revision.new")}</DialogTitle><DialogDescription>{selectedCatalog?.name}</DialogDescription></DialogHeader><div className="space-y-3"><div className="grid grid-cols-2 gap-3"><div><Label>{t("materialRates.revision.effectiveFrom")} *</Label><Input data-testid="material-rates-effective-from" type="date" value={effectiveFrom} onChange={(event) => setEffectiveFrom(event.target.value)} /></div><div><Label>{t("materialRates.revision.effectiveTo")}</Label><Input data-testid="material-rates-effective-to" type="date" value={effectiveTo} min={effectiveFrom} onChange={(event) => setEffectiveTo(event.target.value)} /></div></div><div><Label>{t("materialRates.revision.note")}</Label><Textarea data-testid="material-rates-revision-note" maxLength={1000} value={revisionNote} onChange={(event) => setRevisionNote(event.target.value)} /></div>{formError && <p className="text-sm text-destructive">{formError}</p>}</div><DialogFooter><Button variant="outline" onClick={() => setRevisionOpen(false)}>{t("common.cancel")}</Button><Button data-testid="material-rates-revision-save" onClick={() => void createRevision()} disabled={revisionSaving}>{revisionSaving && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.save")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={importConfirmOpen} onOpenChange={setImportConfirmOpen}><DialogContent><DialogHeader><DialogTitle>{t("materialRates.import.confirmTitle")}</DialogTitle><DialogDescription>{t("materialRates.import.confirmDescription", { file: pendingImportFile?.name ?? "", version: selectedRevision?.version ?? "" })}</DialogDescription></DialogHeader><div className="rounded-md border border-amber-300/70 bg-amber-50 p-3 text-sm leading-relaxed text-amber-950 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-100">{t("materialRates.import.replaceWarning")}</div><DialogFooter><Button variant="outline" onClick={() => setImportConfirmOpen(false)}>{t("common.cancel")}</Button><Button data-testid="material-rates-import-confirm" onClick={() => void importCsv()} disabled={importing}>{importing && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("materialRates.import.confirmAction")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={deleteCatalogOpen} onOpenChange={(open) => { if (!deleteCatalogBusy) setDeleteCatalogOpen(open); }}><DialogContent><DialogHeader><DialogTitle>{t("materialRates.catalog.deleteTitle")}</DialogTitle><DialogDescription>{t("materialRates.catalog.deleteConfirm", { name: selectedCatalog?.name ?? "" })}</DialogDescription></DialogHeader><div className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{t("materialRates.catalog.deleteWarning")}</div>{deleteCatalogError && <p className="text-sm text-destructive" data-testid="material-rates-delete-error">{deleteCatalogError}</p>}<DialogFooter><Button variant="outline" onClick={() => setDeleteCatalogOpen(false)} disabled={deleteCatalogBusy}>{t("common.cancel")}</Button><Button variant="destructive" data-testid="material-rates-delete-confirm" onClick={() => void deleteSelectedCatalog()} disabled={deleteCatalogBusy}>{deleteCatalogBusy && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("materialRates.catalog.delete")}</Button></DialogFooter></DialogContent></Dialog>

      <Dialog open={decision !== null} onOpenChange={(open) => !open && setDecision(null)}><DialogContent><DialogHeader><DialogTitle>{decision && t(`materialRates.action.${decision}`)}</DialogTitle><DialogDescription>{t("materialRates.action.decisionHint")}</DialogDescription></DialogHeader><div><Label>{t("materialRates.revision.decisionNote")}{decision === "reject" ? " *" : ""}</Label><Textarea maxLength={1000} value={decisionNote} onChange={(event) => setDecisionNote(event.target.value)} /></div>{formError && <p className="text-sm text-destructive">{formError}</p>}<DialogFooter><Button variant="outline" onClick={() => setDecision(null)}>{t("common.cancel")}</Button><Button data-testid="material-rates-decision-confirm" onClick={() => void runDecision()} disabled={decisionBusy}>{decisionBusy && <Loader2 className="mr-1 h-4 w-4 animate-spin" />}{t("common.confirm")}</Button></DialogFooter></DialogContent></Dialog>
    </AdminLayout>
  );
};

export default AdminMaterialRates;
