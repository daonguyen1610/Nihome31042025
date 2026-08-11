import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ArrowLeft, Building2, Download, FileText, History, Mail, MapPin, Pencil, Phone, Plus, ShieldCheck, Trash2, Upload, UserRound } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { adminApi, type MasterDataOption } from "@/services/adminApi";
import {
  vendorApi,
  type UpsertVendorEvaluationRequest,
  type VendorAuditResponse,
  type VendorDocumentResponse,
  type VendorDocumentType,
  type VendorEvaluationResponse,
  type VendorProjectOptionResponse,
  type VendorResponse,
} from "@/services/vendorApi";
import VendorEvaluationDialog from "./VendorEvaluationDialog";

const DOCUMENT_TYPES: VendorDocumentType[] = ["Capability", "License", "Other"];
const MAX_FILE_SIZE = 20 * 1024 * 1024;
const ALLOWED_EXTENSIONS = new Set(["pdf", "doc", "docx", "xls", "xlsx", "png", "jpg", "jpeg"]);

const formatDateTime = (value: string, lang: string) => {
  try {
    return new Date(value).toLocaleString(lang);
  } catch {
    return value;
  }
};

const formatFileSize = (bytes: number, t: (key: string) => string) => {
  if (bytes < 1024) return `${bytes} ${t("vendors.document.unit.bytes")}`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} ${t("vendors.document.unit.kilobytes")}`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} ${t("vendors.document.unit.megabytes")}`;
};

const parseAuditJson = (value?: string | null) => {
  if (!value) return null;
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
};

type DeleteTarget = { kind: "document"; item: VendorDocumentResponse } | { kind: "evaluation"; item: VendorEvaluationResponse } | null;

const VendorDetailPage = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const { id: idParam } = useParams<{ id: string }>();
  const vendorId = Number(idParam);
  const validId = Number.isInteger(vendorId) && vendorId > 0;
  const canManage = has(ADMIN_PERMS.vendorsManage);
  const canEvaluate = has(ADMIN_PERMS.vendorsEvaluate);

  const [vendor, setVendor] = useState<VendorResponse | null>(null);
  const [history, setHistory] = useState<VendorAuditResponse[]>([]);
  const [serviceGroups, setServiceGroups] = useState<MasterDataOption[]>([]);
  const [projects, setProjects] = useState<VendorProjectOptionResponse[]>([]);
  const [loading, setLoading] = useState(validId);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(!validId);

  const fetchDetail = useCallback(async () => {
    if (!validId) return;
    setError(null);
    try {
      const [vendorResponse, historyResponse, groupsResponse, projectsResponse] = await Promise.all([
        vendorApi.get(vendorId),
        vendorApi.history(vendorId),
        adminApi.getMasterDataOptions("vendor_service_group").catch(() => ({ data: [] as MasterDataOption[] })),
        canEvaluate ? vendorApi.projectOptions().catch(() => ({ data: [] as VendorProjectOptionResponse[] })) : Promise.resolve(null),
      ]);
      setVendor(vendorResponse.data);
      setHistory(historyResponse.data ?? []);
      setServiceGroups(groupsResponse.data ?? []);
      setProjects(projectsResponse?.data ?? []);
      setNotFound(false);
    } catch (err) {
      if ((err as { response?: { status?: number } }).response?.status === 404) {
        setVendor(null);
        setNotFound(true);
      } else setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [canEvaluate, validId, vendorId]);

  useEffect(() => {
    void fetchDetail();
  }, [fetchDetail]);

  const serviceGroupLabel = useMemo(() => {
    if (!vendor) return "";
    const option = serviceGroups.find((item) => item.code === vendor.serviceGroupCode);
    if (!option) return vendor.serviceGroupCode;
    if (option.labelKey) {
      const translated = t(option.labelKey);
      if (translated !== option.labelKey) return translated;
    }
    return option.name;
  }, [serviceGroups, t, vendor]);

  const [documentType, setDocumentType] = useState<VendorDocumentType>("Capability");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [documentError, setDocumentError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const selectFile = (file?: File) => {
    setDocumentError(null);
    if (!file) {
      setSelectedFile(null);
      return;
    }
    const extension = file.name.split(".").pop()?.toLowerCase() ?? "";
    if (!ALLOWED_EXTENSIONS.has(extension)) {
      setSelectedFile(null);
      setDocumentError(t("vendors.document.validation.extension"));
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      setSelectedFile(null);
      setDocumentError(t("vendors.document.validation.size"));
      return;
    }
    setSelectedFile(file);
  };

  const uploadDocument = async () => {
    if (!selectedFile) {
      setDocumentError(t("vendors.document.validation.required"));
      return;
    }
    setUploading(true);
    setDocumentError(null);
    try {
      await vendorApi.uploadDocument(vendorId, documentType, selectedFile);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";
      toast({ title: t("vendors.document.uploaded") });
      await fetchDetail();
    } catch (err) {
      setDocumentError(extractApiError(err));
    } finally {
      setUploading(false);
    }
  };

  const downloadDocument = async (document: VendorDocumentResponse) => {
    try {
      const response = await vendorApi.downloadDocument(vendorId, document.id);
      const url = window.URL.createObjectURL(response.data);
      const anchor = window.document.createElement("a");
      anchor.href = url;
      anchor.download = document.originalFileName;
      anchor.style.display = "none";
      window.document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const [evaluationOpen, setEvaluationOpen] = useState(false);
  const [editingEvaluation, setEditingEvaluation] = useState<VendorEvaluationResponse | null>(null);
  const [evaluationSaving, setEvaluationSaving] = useState(false);
  const [evaluationError, setEvaluationError] = useState<string | null>(null);

  const openEvaluation = (evaluation: VendorEvaluationResponse | null) => {
    setEditingEvaluation(evaluation);
    setEvaluationError(null);
    setEvaluationOpen(true);
  };

  const saveEvaluation = async (request: UpsertVendorEvaluationRequest) => {
    setEvaluationSaving(true);
    setEvaluationError(null);
    try {
      if (editingEvaluation) await vendorApi.updateEvaluation(vendorId, editingEvaluation.id, request);
      else await vendorApi.createEvaluation(vendorId, request);
      toast({ title: t(editingEvaluation ? "vendors.evaluation.updated" : "vendors.evaluation.created") });
      setEvaluationOpen(false);
      await fetchDetail();
    } catch (err) {
      setEvaluationError(extractApiError(err));
    } finally {
      setEvaluationSaving(false);
    }
  };

  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget>(null);
  const [deleting, setDeleting] = useState(false);
  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      if (deleteTarget.kind === "document") await vendorApi.deleteDocument(vendorId, deleteTarget.item.id);
      else await vendorApi.deleteEvaluation(vendorId, deleteTarget.item.id);
      toast({ title: t(deleteTarget.kind === "document" ? "vendors.document.deleted" : "vendors.evaluation.deleted") });
      setDeleteTarget(null);
      await fetchDetail();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setDeleting(false);
    }
  };

  if (!validId || notFound) return <AdminLayout><div className="p-4"><PageError message={t("vendors.notFound")} /></div></AdminLayout>;
  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><div className="p-4"><PageError message={error} onRetry={() => void fetchDetail()} /></div></AdminLayout>;
  if (!vendor) return <AdminLayout><div className="p-4"><PageError message={t("vendors.notFound")} /></div></AdminLayout>;

  const valueOrFallback = (value?: string | null) => value || t("vendors.value.notAvailable");

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4" data-testid="vendor-detail-page">
        <Link to="/admin/procurement/vendors" className="inline-flex items-center gap-1 text-sm text-slate-600 hover:text-slate-900"><ArrowLeft className="h-4 w-4" />{t("vendors.detail.back")}</Link>
        <header className="rounded-lg border bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div><div className="flex flex-wrap items-center gap-2"><h1 className="text-xl font-bold text-slate-900 md:text-2xl">{vendor.companyName}</h1><Badge variant="outline" className={cn(vendor.isActive ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-slate-200 bg-slate-50 text-slate-600")}>{t(vendor.isActive ? "vendors.status.active" : "vendors.status.inactive")}</Badge></div><p className="mt-1 text-sm text-slate-600">{vendor.vendorCode} · {t(`vendors.type.${vendor.vendorType}`)}</p></div>
            {canManage ? <Button asChild><Link to={`/admin/procurement/vendors/${vendor.id}/edit`}><Pencil className="mr-2 h-4 w-4" />{t("common.edit")}</Link></Button> : null}
          </div>
        </header>

        <div className="grid gap-4 lg:grid-cols-3">
          <section className="rounded-lg border bg-white p-4 shadow-sm"><div className="mb-3 flex items-center gap-2"><Building2 className="h-5 w-5 text-slate-500" /><h2 className="font-semibold">{t("vendors.detail.summary")}</h2></div><dl className="space-y-3 text-sm"><Info label={t("vendors.field.vendorCode")} value={vendor.vendorCode} /><Info label={t("vendors.field.vendorType")} value={t(`vendors.type.${vendor.vendorType}`)} /><Info label={t("vendors.field.serviceGroup")} value={serviceGroupLabel} /><Info label={t("vendors.field.owner")} value={vendor.ownerName} /><Info label={t("vendors.field.averageScore")} value={vendor.averageScore == null ? t("vendors.value.notAvailable") : vendor.averageScore.toFixed(2)} /></dl></section>
          <section className="rounded-lg border bg-white p-4 shadow-sm"><div className="mb-3 flex items-center gap-2"><UserRound className="h-5 w-5 text-slate-500" /><h2 className="font-semibold">{t("vendors.detail.contact")}</h2></div><dl className="space-y-3 text-sm"><Info icon={<UserRound className="h-4 w-4" />} label={t("vendors.field.contactPerson")} value={valueOrFallback(vendor.contactPerson)} /><Info icon={<Phone className="h-4 w-4" />} label={t("vendors.field.phone")} value={valueOrFallback(vendor.phone)} /><Info icon={<Mail className="h-4 w-4" />} label={t("vendors.field.email")} value={valueOrFallback(vendor.email)} /><Info icon={<MapPin className="h-4 w-4" />} label={t("vendors.field.address")} value={valueOrFallback(vendor.address)} /></dl></section>
          <section className="rounded-lg border bg-white p-4 shadow-sm"><div className="mb-3 flex items-center gap-2"><ShieldCheck className="h-5 w-5 text-slate-500" /><h2 className="font-semibold">{t("vendors.detail.compliance")}</h2></div><dl className="space-y-3 text-sm"><Info label={t("vendors.field.taxCode")} value={valueOrFallback(vendor.taxCode)} /><Info label={t("vendors.field.licenseNo")} value={valueOrFallback(vendor.licenseNo)} /><Info label={t("vendors.field.createdAt")} value={formatDateTime(vendor.createdAt, lang)} /><Info label={t("vendors.field.updatedAt")} value={formatDateTime(vendor.updatedAt, lang)} /></dl></section>
        </div>

        <section className="rounded-lg border bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="font-semibold">{t("vendors.document.title")}</h2><p className="mt-1 text-xs text-slate-500">{t("vendors.document.guidance")}</p></div></div>
          {canManage ? <div className="mt-4 grid gap-3 rounded-md border bg-slate-50 p-3 md:grid-cols-[180px_1fr_auto]"><div className="space-y-1"><Label>{t("vendors.document.type")}</Label><Select value={documentType} onValueChange={(value) => setDocumentType(value as VendorDocumentType)}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{DOCUMENT_TYPES.map((value) => <SelectItem key={value} value={value}>{t(`vendors.document.type.${value}`)}</SelectItem>)}</SelectContent></Select></div><div className="space-y-1"><Label htmlFor="vendor-document-file">{t("vendors.document.file")}</Label><input ref={fileInputRef} id="vendor-document-file" type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg" onChange={(event) => selectFile(event.target.files?.[0])} className="block w-full rounded-md border bg-white px-3 py-2 text-sm file:mr-3 file:rounded file:border-0 file:bg-slate-100 file:px-2 file:py-1" /><p className="text-xs text-slate-500">{selectedFile ? selectedFile.name : t("vendors.document.noFileSelected")}</p></div><Button type="button" className="self-end" disabled={uploading} onClick={() => void uploadDocument()}><Upload className="mr-2 h-4 w-4" />{t(uploading ? "vendors.document.uploading" : "vendors.document.upload")}</Button>{documentError ? <p role="alert" className="text-sm text-destructive md:col-span-3">{documentError}</p> : null}</div> : null}
          <div className="mt-4 space-y-2">{vendor.documents.length === 0 ? <PageEmpty message={t("vendors.document.empty")} /> : vendor.documents.map((document) => <article key={document.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-3"><div className="flex min-w-0 items-center gap-3"><FileText className="h-5 w-5 shrink-0 text-slate-500" /><div className="min-w-0"><p className="truncate text-sm font-medium">{document.originalFileName}</p><p className="text-xs text-slate-500">{t(`vendors.document.type.${document.documentType}`)} · {formatFileSize(document.fileSizeBytes, t)} · {formatDateTime(document.createdAt, lang)}</p></div></div><div className="flex gap-1"><Button type="button" size="sm" variant="outline" onClick={() => void downloadDocument(document)}><Download className="mr-1 h-4 w-4" />{t("vendors.document.download")}</Button>{canManage ? <Button type="button" size="icon" variant="ghost" title={t("vendors.document.delete")} aria-label={t("vendors.document.delete")} onClick={() => setDeleteTarget({ kind: "document", item: document })}><Trash2 className="h-4 w-4 text-destructive" /></Button> : null}</div></article>)}</div>
        </section>

        <section className="rounded-lg border bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="font-semibold">{t("vendors.evaluation.title")}</h2><p className="mt-1 text-xs text-slate-500">{t("vendors.evaluation.subtitle")}</p></div>{canEvaluate ? <Button size="sm" onClick={() => openEvaluation(null)}><Plus className="mr-1 h-4 w-4" />{t("vendors.evaluation.add")}</Button> : null}</div>
          <div className="mt-4 space-y-3">{vendor.evaluations.length === 0 ? <PageEmpty message={t("vendors.evaluation.empty")} /> : vendor.evaluations.map((evaluation) => <article key={evaluation.id} className="rounded-md border p-3"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-medium">{evaluation.projectCode} · {evaluation.projectName}</p><p className="text-xs text-slate-500">{t("vendors.evaluation.evaluatedBy").replace("{name}", evaluation.evaluatorName).replace("{date}", formatDateTime(evaluation.evaluatedAt, lang))}</p></div><div className="flex items-center gap-2"><Badge variant="outline" className="border-amber-200 bg-amber-50 text-amber-700">{evaluation.averageScore.toFixed(2)}</Badge>{canEvaluate ? <><Button type="button" size="icon" variant="ghost" title={t("common.edit")} aria-label={t("common.edit")} onClick={() => openEvaluation(evaluation)}><Pencil className="h-4 w-4" /></Button><Button type="button" size="icon" variant="ghost" title={t("common.delete")} aria-label={t("common.delete")} onClick={() => setDeleteTarget({ kind: "evaluation", item: evaluation })}><Trash2 className="h-4 w-4 text-destructive" /></Button></> : null}</div></div><div className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">{(["scoreQuality", "scoreSchedule", "scoreCost", "scoreSafety"] as const).map((field) => <div key={field} className="rounded bg-slate-50 p-2"><span className="block text-xs text-slate-500">{t(`vendors.evaluation.${field}`)}</span><strong>{evaluation[field]}</strong></div>)}</div>{evaluation.comment ? <p className="mt-3 whitespace-pre-wrap text-sm text-slate-700">{evaluation.comment}</p> : null}<p className="mt-2 text-xs text-slate-500">{t("vendors.evaluation.updatedBy").replace("{name}", evaluation.updatedByName).replace("{date}", formatDateTime(evaluation.updatedAt, lang))}</p></article>)}</div>
        </section>

        <section className="rounded-lg border bg-white p-4 shadow-sm"><div className="mb-4 flex items-center gap-2"><History className="h-5 w-5 text-slate-500" /><div><h2 className="font-semibold">{t("vendors.history.title")}</h2><p className="text-xs text-slate-500">{t("vendors.history.subtitle")}</p></div></div>{history.length === 0 ? <PageEmpty message={t("vendors.history.empty")} /> : <ol className="space-y-3">{history.map((entry) => { const oldValue = parseAuditJson(entry.oldValueJson); const newValue = parseAuditJson(entry.newValueJson); return <li key={entry.id} className="relative border-l-2 border-slate-200 pl-4"><span className="absolute -left-[5px] top-1 h-2 w-2 rounded-full bg-slate-400" /><div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-medium">{t(`vendors.history.action.${entry.action}`)}</p><time className="text-xs text-slate-500">{formatDateTime(entry.createdAt, lang)}</time></div><p className="text-xs text-slate-500">{entry.actorPhone || t("vendors.history.systemActor")}</p>{oldValue || newValue ? <details className="mt-2 rounded border bg-slate-50 p-2"><summary className="cursor-pointer text-xs font-medium">{t("vendors.history.viewChanges")}</summary><div className="mt-2 grid gap-2 lg:grid-cols-2">{oldValue ? <div><p className="mb-1 text-xs text-slate-500">{t("vendors.history.oldValue")}</p><pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all rounded bg-white p-2 text-xs">{oldValue}</pre></div> : null}{newValue ? <div><p className="mb-1 text-xs text-slate-500">{t("vendors.history.newValue")}</p><pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all rounded bg-white p-2 text-xs">{newValue}</pre></div> : null}</div></details> : null}</li>; })}</ol>}</section>
      </div>

      <VendorEvaluationDialog open={evaluationOpen} onOpenChange={setEvaluationOpen} evaluation={editingEvaluation} projects={projects} saving={evaluationSaving} apiError={evaluationError} onSave={saveEvaluation} />
      <AlertDialog open={Boolean(deleteTarget)} onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{t(deleteTarget?.kind === "document" ? "vendors.document.deleteTitle" : "vendors.evaluation.deleteTitle")}</AlertDialogTitle><AlertDialogDescription>{t(deleteTarget?.kind === "document" ? "vendors.document.deleteConfirm" : "vendors.evaluation.deleteConfirm")}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel><AlertDialogAction disabled={deleting} onClick={() => void confirmDelete()}>{t(deleting ? "vendors.action.deleting" : "common.delete")}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </AdminLayout>
  );
};

const Info = ({ label, value, icon }: { label: string; value: string; icon?: React.ReactNode }) => <div><dt className="flex items-center gap-1 text-xs text-slate-500">{icon}{label}</dt><dd className="mt-0.5 break-words text-slate-900">{value}</dd></div>;

export default VendorDetailPage;
