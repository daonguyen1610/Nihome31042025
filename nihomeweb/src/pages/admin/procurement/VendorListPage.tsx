import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowDown, ArrowUp, ArrowUpDown, Building2, Eye, Pencil, Plus, Search } from "lucide-react";
import { Link } from "react-router-dom";
import AdminExportButton from "@/components/admin/AdminExportButton";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { adminApi, type MasterDataOption } from "@/services/adminApi";
import {
  vendorApi,
  type SortDirection,
  type VendorListParams,
  type VendorResponse,
  type VendorOwnerOptionResponse,
  type VendorSortField,
  type VendorType,
} from "@/services/vendorApi";

const PAGE_SIZE = 20;
const VENDOR_TYPES: VendorType[] = ["Supplier", "SubContractor", "Both"];
const SORT_FIELDS: VendorSortField[] = ["vendorCode", "companyName", "vendorType", "ownerName", "averageScore", "updatedAt"];

const formatDate = (value: string, lang: string) => {
  try {
    return new Date(value).toLocaleDateString(lang);
  } catch {
    return value;
  }
};

const VendorListPage = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.vendorsManage);
  const canExport = has(ADMIN_PERMS.vendorsExport);
  const canSeeAll = has(ADMIN_PERMS.vendorsViewAll);

  const [rows, setRows] = useState<VendorResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [serviceGroups, setServiceGroups] = useState<MasterDataOption[]>([]);
  const [owners, setOwners] = useState<VendorOwnerOptionResponse[]>([]);
  const [ownerOptionsAvailable, setOwnerOptionsAvailable] = useState(false);

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [type, setType] = useState<VendorType | "">("");
  const [status, setStatus] = useState<"active" | "inactive" | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [serviceGroupCode, setServiceGroupCode] = useState("");
  const [sortBy, setSortBy] = useState<VendorSortField>("companyName");
  const [sortDirection, setSortDirection] = useState<SortDirection>("asc");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 350);
    return () => window.clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    let cancelled = false;
    void adminApi.getMasterDataOptions("vendor_service_group").then(({ data }) => {
      if (!cancelled) setServiceGroups(data.filter((item) => item.isActive));
    }).catch(() => undefined);
    if (canSeeAll) {
      void vendorApi.ownerOptions().then(({ data }) => {
        if (!cancelled) {
          setOwners(data ?? []);
          setOwnerOptionsAvailable(true);
        }
      }).catch(() => {
        if (!cancelled) setOwnerOptionsAvailable(false);
      });
    }
    return () => {
      cancelled = true;
    };
  }, [canSeeAll]);

  const queryParams = useMemo<VendorListParams>(() => ({
    search: search || undefined,
    type: type || undefined,
    isActive: status ? status === "active" : undefined,
    ownerUserId: canSeeAll && ownerOptionsAvailable ? ownerUserId ?? undefined : undefined,
    serviceGroupCode: serviceGroupCode || undefined,
    sortBy,
    sortDirection,
    page,
    pageSize: PAGE_SIZE,
  }), [search, type, status, ownerUserId, serviceGroupCode, sortBy, sortDirection, page, canSeeAll, ownerOptionsAvailable]);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await vendorApi.list(queryParams);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [queryParams]);

  useEffect(() => {
    void fetchRows();
  }, [fetchRows]);

  const serviceGroupLabel = useCallback((code: string) => {
    const option = serviceGroups.find((item) => item.code === code);
    if (!option) return code;
    if (option.labelKey) {
      const translated = t(option.labelKey);
      if (translated !== option.labelKey) return translated;
    }
    return option.name;
  }, [serviceGroups, t]);

  const handleSort = (field: VendorSortField) => {
    if (sortBy === field) setSortDirection((current) => current === "asc" ? "desc" : "asc");
    else {
      setSortBy(field);
      setSortDirection("asc");
    }
    setPage(1);
  };

  const renderSortIcon = (field: VendorSortField) => {
    if (sortBy !== field) return <ArrowUpDown className="h-3.5 w-3.5" />;
    return sortDirection === "asc" ? <ArrowUp className="h-3.5 w-3.5" /> : <ArrowDown className="h-3.5 w-3.5" />;
  };

  const exportRows = async () => {
    setExporting(true);
    try {
      const { data } = await vendorApi.export(queryParams);
      downloadCsv({
        filename: createCsvFilename("procurement-vendors"),
        rows: data,
        columns: [
          { header: t("vendors.field.vendorCode"), value: "vendorCode" },
          { header: t("vendors.field.companyName"), value: "companyName" },
          { header: t("vendors.field.vendorType"), value: (row) => t(`vendors.type.${row.vendorType}`) },
          { header: t("vendors.field.taxCode"), value: "taxCode" },
          { header: t("vendors.field.phone"), value: "phone" },
          { header: t("vendors.field.email"), value: "email" },
          { header: t("vendors.field.address"), value: "address" },
          { header: t("vendors.field.contactPerson"), value: "contactPerson" },
          { header: t("vendors.field.licenseNo"), value: "licenseNo" },
          { header: t("vendors.field.serviceGroup"), value: (row) => serviceGroupLabel(row.serviceGroupCode) },
          { header: t("vendors.field.owner"), value: "ownerName" },
          { header: t("vendors.field.status"), value: (row) => t(row.isActive ? "vendors.status.active" : "vendors.status.inactive") },
          { header: t("vendors.field.averageScore"), value: "averageScore" },
          { header: t("vendors.field.updatedAt"), value: (row) => formatDate(row.updatedAt, lang) },
        ],
      });
      toast({ title: t("vendors.export.success") });
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setExporting(false);
    }
  };

  const resetFilters = () => {
    setSearchInput("");
    setSearch("");
    setType("");
    setStatus("");
    setOwnerUserId(null);
    setServiceGroupCode("");
    setPage(1);
  };

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const hasFilters = Boolean(search || type || status || ownerUserId || serviceGroupCode);

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4" data-testid="vendor-list-page">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 className="text-xl font-bold text-slate-900 md:text-2xl">{t("vendors.title")}</h1>
            <p className="mt-1 text-sm text-slate-600">{t("vendors.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {canExport ? <AdminExportButton onClick={() => void exportRows()} disabled={exporting || total === 0} label={t("vendors.export.action")} /> : null}
            {canManage ? (
              <Button asChild>
                <Link to="/admin/procurement/vendors/new"><Plus className="mr-2 h-4 w-4" />{t("vendors.action.create")}</Link>
              </Button>
            ) : null}
          </div>
        </header>

        <section className="rounded-lg border bg-white p-3 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
            <div className="space-y-1 xl:col-span-2">
              <Label htmlFor="vendor-search">{t("vendors.filter.search")}</Label>
              <div className="relative">
                <Search className="absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
                <Input id="vendor-search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} placeholder={t("vendors.filter.searchPlaceholder")} className="pl-9" />
              </div>
            </div>
            <div className="space-y-1">
              <Label>{t("vendors.filter.type")}</Label>
              <Select value={type || "all"} onValueChange={(value) => { setType(value === "all" ? "" : value as VendorType); setPage(1); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent><SelectItem value="all">{t("vendors.filter.allTypes")}</SelectItem>{VENDOR_TYPES.map((value) => <SelectItem key={value} value={value}>{t(`vendors.type.${value}`)}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>{t("vendors.filter.status")}</Label>
              <Select value={status || "all"} onValueChange={(value) => { setStatus(value === "all" ? "" : value as "active" | "inactive"); setPage(1); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent><SelectItem value="all">{t("vendors.filter.allStatuses")}</SelectItem><SelectItem value="active">{t("vendors.status.active")}</SelectItem><SelectItem value="inactive">{t("vendors.status.inactive")}</SelectItem></SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>{t("vendors.filter.serviceGroup")}</Label>
              <Select value={serviceGroupCode || "all"} onValueChange={(value) => { setServiceGroupCode(value === "all" ? "" : value); setPage(1); }}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent><SelectItem value="all">{t("vendors.filter.allServiceGroups")}</SelectItem>{serviceGroups.map((item) => <SelectItem key={item.id} value={item.code}>{serviceGroupLabel(item.code)}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            {canSeeAll && ownerOptionsAvailable ? (
              <div className="space-y-1">
                <Label>{t("vendors.filter.owner")}</Label>
                <Select value={ownerUserId ? String(ownerUserId) : "all"} onValueChange={(value) => { setOwnerUserId(value === "all" ? null : Number(value)); setPage(1); }}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent><SelectItem value="all">{t("vendors.filter.allOwners")}</SelectItem>{owners.map((owner) => <SelectItem key={owner.id} value={String(owner.id)}>{owner.fullName || owner.email || owner.phoneNumber}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            ) : null}
          </div>
          {hasFilters ? <Button type="button" variant="ghost" size="sm" className="mt-2" onClick={resetFilters}>{t("vendors.filter.reset")}</Button> : null}
        </section>

        {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => void fetchRows()} /> : rows.length === 0 ? <PageEmpty message={t("vendors.empty")} /> : (
          <>
            <div className="hidden overflow-hidden rounded-lg border bg-white shadow-sm md:block">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      {SORT_FIELDS.slice(0, 5).map((field) => <th key={field} className="px-3 py-3"><button type="button" className="inline-flex items-center gap-1" onClick={() => handleSort(field)}>{t(`vendors.sort.${field}`)}{renderSortIcon(field)}</button></th>)}
                      <th className="px-3 py-3">{t("vendors.field.status")}</th>
                      <th className="px-3 py-3 text-right">{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {rows.map((vendor) => (
                      <tr key={vendor.id} className="hover:bg-slate-50">
                        <td className="px-3 py-3 font-medium text-slate-900">{vendor.vendorCode}</td>
                        <td className="px-3 py-3"><Link className="font-medium text-primary hover:underline" to={`/admin/procurement/vendors/${vendor.id}`}>{vendor.companyName}</Link><p className="text-xs text-slate-500">{serviceGroupLabel(vendor.serviceGroupCode)}</p></td>
                        <td className="px-3 py-3">{t(`vendors.type.${vendor.vendorType}`)}</td>
                        <td className="px-3 py-3">{vendor.ownerName}</td>
                        <td className="px-3 py-3">{vendor.averageScore == null ? t("vendors.value.notAvailable") : vendor.averageScore.toFixed(2)}</td>
                        <td className="px-3 py-3"><Badge variant="outline" className={cn(vendor.isActive ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-slate-200 bg-slate-50 text-slate-600")}>{t(vendor.isActive ? "vendors.status.active" : "vendors.status.inactive")}</Badge></td>
                        <td className="px-3 py-3"><div className="flex justify-end gap-1"><Button asChild variant="ghost" size="icon" title={t("vendors.action.view")}><Link to={`/admin/procurement/vendors/${vendor.id}`} aria-label={t("vendors.action.view")}><Eye className="h-4 w-4" /></Link></Button>{canManage ? <Button asChild variant="ghost" size="icon" title={t("common.edit")}><Link to={`/admin/procurement/vendors/${vendor.id}/edit`} aria-label={t("common.edit")}><Pencil className="h-4 w-4" /></Link></Button> : null}</div></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="space-y-3 md:hidden">
              {rows.map((vendor) => <article key={vendor.id} className="rounded-lg border bg-white p-4 shadow-sm"><div className="flex items-start justify-between gap-3"><div className="min-w-0"><div className="flex items-center gap-2"><Building2 className="h-4 w-4 shrink-0 text-slate-500" /><p className="truncate font-semibold">{vendor.companyName}</p></div><p className="mt-1 text-xs text-slate-500">{vendor.vendorCode}</p></div><Badge variant="outline" className={cn(vendor.isActive ? "border-emerald-200 bg-emerald-50 text-emerald-700" : "border-slate-200 bg-slate-50 text-slate-600")}>{t(vendor.isActive ? "vendors.status.active" : "vendors.status.inactive")}</Badge></div><dl className="mt-3 grid grid-cols-2 gap-3 text-sm"><div><dt className="text-xs text-slate-500">{t("vendors.field.vendorType")}</dt><dd>{t(`vendors.type.${vendor.vendorType}`)}</dd></div><div><dt className="text-xs text-slate-500">{t("vendors.field.averageScore")}</dt><dd>{vendor.averageScore == null ? t("vendors.value.notAvailable") : vendor.averageScore.toFixed(2)}</dd></div><div><dt className="text-xs text-slate-500">{t("vendors.field.owner")}</dt><dd>{vendor.ownerName}</dd></div><div><dt className="text-xs text-slate-500">{t("vendors.field.updatedAt")}</dt><dd>{formatDate(vendor.updatedAt, lang)}</dd></div></dl><div className="mt-3 flex gap-2"><Button asChild variant="outline" size="sm"><Link to={`/admin/procurement/vendors/${vendor.id}`}><Eye className="mr-1 h-4 w-4" />{t("vendors.action.view")}</Link></Button>{canManage ? <Button asChild variant="outline" size="sm"><Link to={`/admin/procurement/vendors/${vendor.id}/edit`}><Pencil className="mr-1 h-4 w-4" />{t("common.edit")}</Link></Button> : null}</div></article>)}
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border bg-white px-3 py-2 text-sm">
              <span>{t("vendors.pagination.summary").replace("{count}", String(rows.length)).replace("{total}", String(total))}</span>
              <div className="flex items-center gap-2"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>{t("vendors.pagination.previous")}</Button><span>{t("vendors.pagination.page").replace("{page}", String(page)).replace("{pages}", String(totalPages))}</span><Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>{t("common.next")}</Button></div>
            </div>
          </>
        )}
      </div>
    </AdminLayout>
  );
};

export default VendorListPage;
