import { useCallback, useEffect, useState } from "react";
import { ArrowDownAZ, ArrowUpAZ, Eye, Plus, Search } from "lucide-react";
import { useNavigate } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { useI18n } from "@/lib/i18n";
import { adminApi, type CreateVendorRequest, type VendorListParams, type VendorResponse, type VendorType } from "@/services/adminApi";
import VendorForm from "./VendorForm";

const TYPES: VendorType[] = ["Supplier", "SubContractor", "Both"];

export default function VendorPage() {
  const { t } = useI18n();
  const { has } = usePermissions();
  const { toast } = useToast();
  const navigate = useNavigate();
  const canManage = has(ADMIN_PERMS.vendorsManage);
  const canExport = has(ADMIN_PERMS.vendorsExport);
  const [rows, setRows] = useState<VendorResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [type, setType] = useState<VendorType | "all">("all");
  const [status, setStatus] = useState<"all" | "active" | "inactive">("all");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [exporting, setExporting] = useState(false);
  const pageSize = 20;

  useEffect(() => {
    const timeout = window.setTimeout(() => { setSearch(searchInput.trim()); setPage(1); }, 300);
    return () => window.clearTimeout(timeout);
  }, [searchInput]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: VendorListParams = { page, pageSize, sortBy: "companyName", sortDirection };
      if (type !== "all") params.vendorType = type;
      if (status !== "all") params.isActive = status === "active";
      if (search) params.search = search;
      const { data } = await adminApi.listVendors(params);
      setRows(data.items);
      setTotal(data.total);
    } catch (loadError) {
      setError((loadError as Error).message);
    } finally {
      setLoading(false);
    }
  }, [page, search, sortDirection, status, type]);

  useEffect(() => { void load(); }, [load]);

  const create = async (request: CreateVendorRequest) => {
    await adminApi.createVendor(request);
    setCreating(false);
    toast({ title: t("proc.vendors.created") });
    await load();
  };

  const exportRows = async () => {
    setExporting(true);
    try {
      const params: VendorListParams = { page: 1, pageSize: 100, sortBy: "companyName", sortDirection };
      if (type !== "all") params.vendorType = type;
      if (status !== "all") params.isActive = status === "active";
      if (search) params.search = search;

      const firstPage = (await adminApi.listVendors(params)).data;
      const exportData = [...firstPage.items];
      const pageCount = Math.ceil(firstPage.total / firstPage.pageSize);
      for (let exportPage = 2; exportPage <= pageCount; exportPage += 1) {
        const response = await adminApi.listVendors({ ...params, page: exportPage });
        exportData.push(...response.data.items);
      }

      downloadCsv({
        filename: createCsvFilename("vendors"),
        rows: exportData,
        columns: [
          { header: t("proc.vendors.field.code"), value: "vendorCode" },
          { header: t("proc.vendors.field.companyName"), value: "companyName" },
          { header: t("proc.vendors.field.type"), value: (row) => t(`proc.vendors.type.${row.vendorType}`) },
          { header: t("proc.vendors.field.taxCode"), value: "taxCode" },
          { header: t("proc.vendors.field.contactPerson"), value: "contactPerson" },
          { header: t("proc.vendors.field.phone"), value: "phone" },
          { header: t("proc.vendors.field.email"), value: "email" },
          { header: t("proc.vendors.field.status"), value: (row) => t(row.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive") },
        ],
      });
    } catch (exportError) {
      toast({ variant: "destructive", title: extractApiError(exportError) || t("common.error") });
    } finally {
      setExporting(false);
    }
  };

  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <AdminLayout>
      <div className="space-y-5 p-4 sm:p-6">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div><h1 className="text-2xl font-semibold">{t("proc.vendors.title")}</h1><p className="text-sm text-muted-foreground">{t("proc.vendors.subtitle")}</p></div>
          <div className="flex gap-2">
            {canExport && <AdminExportButton onClick={() => void exportRows()} disabled={total === 0 || loading || exporting} />}
            {canManage && <Button onClick={() => setCreating(true)} className="gap-2"><Plus className="h-4 w-4" />{t("proc.vendors.action.create")}</Button>}
          </div>
        </div>

        <div className="grid gap-3 rounded-md border bg-card p-3 md:grid-cols-[minmax(220px,1fr)_180px_160px_auto]">
          <div className="relative"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("proc.vendors.searchPlaceholder")} className="pl-9" /></div>
          <Select value={type} onValueChange={(value) => { setType(value as VendorType | "all"); setPage(1); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("proc.vendors.filter.allTypes")}</SelectItem>{TYPES.map((item) => <SelectItem key={item} value={item}>{t(`proc.vendors.type.${item}`)}</SelectItem>)}</SelectContent></Select>
          <Select value={status} onValueChange={(value) => { setStatus(value as typeof status); setPage(1); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("proc.vendors.filter.allStatuses")}</SelectItem><SelectItem value="active">{t("proc.vendors.status.active")}</SelectItem><SelectItem value="inactive">{t("proc.vendors.status.inactive")}</SelectItem></SelectContent></Select>
          <Button variant="outline" title={t("proc.vendors.sort.companyName")} onClick={() => setSortDirection((current) => current === "asc" ? "desc" : "asc")}><span className="sr-only">{t("proc.vendors.sort.companyName")}</span>{sortDirection === "asc" ? <ArrowDownAZ className="h-4 w-4" /> : <ArrowUpAZ className="h-4 w-4" />}</Button>
        </div>

        {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => void load()} /> : rows.length === 0 ? <PageEmpty message={t("proc.vendors.empty")} /> : (
          <>
            <div className="hidden overflow-hidden rounded-md border md:block"><table className="w-full text-sm"><thead className="bg-muted/60 text-left"><tr><th className="px-4 py-3">{t("proc.vendors.field.code")}</th><th className="px-4 py-3">{t("proc.vendors.field.companyName")}</th><th className="px-4 py-3">{t("proc.vendors.field.type")}</th><th className="px-4 py-3">{t("proc.vendors.field.contact")}</th><th className="px-4 py-3">{t("proc.vendors.field.status")}</th><th className="w-16 px-4 py-3"><span className="sr-only">{t("common.actions")}</span></th></tr></thead><tbody className="divide-y">{rows.map((vendor) => <tr key={vendor.id} className="hover:bg-muted/30"><td className="px-4 py-3 font-medium">{vendor.vendorCode}</td><td className="px-4 py-3"><button className="text-left font-medium hover:text-primary" onClick={() => navigate(`/admin/vendors/${vendor.id}`)}>{vendor.companyName}</button><p className="text-xs text-muted-foreground">{vendor.tradeCategory || t("common.noData")}</p></td><td className="px-4 py-3">{t(`proc.vendors.type.${vendor.vendorType}`)}</td><td className="px-4 py-3"><p>{vendor.contactPerson || t("common.noData")}</p><p className="text-xs text-muted-foreground">{vendor.phone || vendor.email || t("common.noData")}</p></td><td className="px-4 py-3"><Badge variant={vendor.isActive ? "default" : "secondary"}>{t(vendor.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive")}</Badge></td><td className="px-4 py-3"><Button variant="ghost" size="icon" title={t("common.view")} onClick={() => navigate(`/admin/vendors/${vendor.id}`)}><Eye className="h-4 w-4" /></Button></td></tr>)}</tbody></table></div>
            <div className="grid gap-3 md:hidden">{rows.map((vendor) => <button key={vendor.id} onClick={() => navigate(`/admin/vendors/${vendor.id}`)} className="rounded-md border bg-card p-4 text-left"><div className="flex items-start justify-between gap-3"><div><p className="font-semibold">{vendor.companyName}</p><p className="text-xs text-muted-foreground">{vendor.vendorCode} · {t(`proc.vendors.type.${vendor.vendorType}`)}</p></div><Badge variant={vendor.isActive ? "default" : "secondary"}>{t(vendor.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive")}</Badge></div><p className="mt-3 text-sm">{vendor.contactPerson || t("common.noData")}</p><p className="text-xs text-muted-foreground">{vendor.phone || vendor.email || t("common.noData")}</p></button>)}</div>
            <div className="flex items-center justify-between"><p className="text-sm text-muted-foreground">{t("proc.vendors.total")} {total}</p><div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>{t("common.prev")}</Button><span className="text-sm">{page} / {pages}</span><Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage((value) => value + 1)}>{t("common.next")}</Button></div></div>
          </>
        )}
      </div>
      <Dialog open={creating} onOpenChange={setCreating}><DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto"><DialogHeader><DialogTitle>{t("proc.vendors.createTitle")}</DialogTitle><DialogDescription>{t("proc.vendors.formDescription")}</DialogDescription></DialogHeader><VendorForm onSubmit={create} onCancel={() => setCreating(false)} /></DialogContent></Dialog>
    </AdminLayout>
  );
}