import { useCallback, useEffect, useState } from "react";
import {
  ArrowDownAZ,
  ArrowRight,
  ArrowUpAZ,
  Eye,
  Pencil,
  Plus,
  Search,
  Trash2,
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type CreateVendorRequest,
  type DeletionImpactResponse,
  type UpdateVendorRequest,
  type VendorListParams,
  type VendorResponse,
  type VendorType,
} from "@/services/adminApi";
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
  const [editing, setEditing] = useState<VendorResponse | null>(null);
  const [pendingDelete, setPendingDelete] = useState<VendorResponse | null>(null);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [loadingImpact, setLoadingImpact] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [exporting, setExporting] = useState(false);
  const pageSize = 20;

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
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

  useEffect(() => {
    void load();
  }, [load]);

  const create = async (request: CreateVendorRequest) => {
    await adminApi.createVendor(request);
    setCreating(false);
    toast({ title: t("proc.vendors.created") });
    await load();
  };

  const update = async (request: UpdateVendorRequest) => {
    if (!editing) return;
    await adminApi.updateVendor(editing.id, request);
    setEditing(null);
    toast({ title: t("proc.vendors.updated") });
    await load();
  };

  const openDelete = async (vendor: VendorResponse) => {
    setPendingDelete(vendor);
    setDeleteImpact(null);
    setDeleteError(null);
    setLoadingImpact(true);
    try {
      setDeleteImpact((await adminApi.getVendorDeletionImpact(vendor.id)).data);
    } catch (impactError) {
      setDeleteError(extractApiError(impactError) || t("common.error"));
    } finally {
      setLoadingImpact(false);
    }
  };

  const deleteVendor = async (confirmation: string) => {
    if (!pendingDelete) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      return (
        await adminApi.deleteVendor(pendingDelete.id, {
          planToken: deleteImpact!.planToken,
          confirmation,
          rowVersion: pendingDelete.rowVersion,
        })
      ).data;
    } catch (deleteError) {
      setDeleteError(extractApiError(deleteError) || t("common.error"));
      throw deleteError;
    } finally {
      setDeleting(false);
    }
  };

  const deleteCompleted = async () => {
    setPendingDelete(null);
    setDeleteImpact(null);
    toast({ title: t("proc.vendors.deleted") });
    if (rows.length === 1 && page > 1) setPage((current) => current - 1);
    else await load();
  };

  const exportRows = async () => {
    setExporting(true);
    try {
      const params: VendorListParams = {
        page: 1,
        pageSize: 100,
        sortBy: "companyName",
        sortDirection,
      };
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
          {
            header: t("proc.vendors.field.type"),
            value: (row) => t(`proc.vendors.type.${row.vendorType}`),
          },
          { header: t("proc.vendors.field.taxCode"), value: "taxCode" },
          { header: t("proc.vendors.field.contactPerson"), value: "contactPerson" },
          { header: t("proc.vendors.field.phone"), value: "phone" },
          { header: t("proc.vendors.field.email"), value: "email" },
          {
            header: t("proc.vendors.field.status"),
            value: (row) =>
              t(row.isActive ? "proc.vendors.status.active" : "proc.vendors.status.inactive"),
          },
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
          <div>
            <h1 className="text-2xl font-semibold sm:text-3xl">{t("proc.vendors.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("proc.vendors.subtitle")}</p>
          </div>
          <div className="flex w-full flex-wrap gap-2 sm:w-auto sm:flex-nowrap">
            {canExport && (
              <AdminExportButton
                className="min-h-11 flex-1 sm:min-h-10 sm:flex-none"
                onClick={() => void exportRows()}
                disabled={total === 0 || loading || exporting}
              />
            )}
            {canManage && (
              <Button
                onClick={() => setCreating(true)}
                className="min-h-11 flex-1 gap-2 sm:min-h-10 sm:flex-none"
              >
                <Plus className="h-4 w-4" />
                {t("proc.vendors.action.create")}
              </Button>
            )}
          </div>
        </div>

        <div className="grid gap-3 rounded-xl border bg-card p-3 shadow-sm sm:grid-cols-2 xl:grid-cols-[minmax(260px,1fr)_180px_160px_max-content]">
          <div className="relative sm:col-span-2 xl:col-span-1">
            <Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
            <Input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder={t("proc.vendors.searchPlaceholder")}
              className="min-h-11 pl-9 sm:min-h-10"
            />
          </div>
          <Select
            value={type}
            onValueChange={(value) => {
              setType(value as VendorType | "all");
              setPage(1);
            }}
          >
            <SelectTrigger className="min-h-11 sm:min-h-10">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("proc.vendors.filter.allTypes")}</SelectItem>
              {TYPES.map((item) => (
                <SelectItem key={item} value={item}>
                  {t(`proc.vendors.type.${item}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={status}
            onValueChange={(value) => {
              setStatus(value as typeof status);
              setPage(1);
            }}
          >
            <SelectTrigger className="min-h-11 sm:min-h-10">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">{t("proc.vendors.filter.allStatuses")}</SelectItem>
              <SelectItem value="active">{t("proc.vendors.status.active")}</SelectItem>
              <SelectItem value="inactive">{t("proc.vendors.status.inactive")}</SelectItem>
            </SelectContent>
          </Select>
          <Button
            className="min-h-11 gap-2 sm:min-h-10"
            variant="outline"
            title={t("proc.vendors.sort.companyName")}
            onClick={() => setSortDirection((current) => (current === "asc" ? "desc" : "asc"))}
          >
            {sortDirection === "asc" ? (
              <ArrowDownAZ className="h-4 w-4" />
            ) : (
              <ArrowUpAZ className="h-4 w-4" />
            )}
            <span>{t("proc.vendors.sort.companyName")}</span>
          </Button>
        </div>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void load()} />
        ) : rows.length === 0 ? (
          <PageEmpty message={t("proc.vendors.empty")} />
        ) : (
          <>
            <div className="hidden overflow-x-auto rounded-xl border bg-card shadow-sm lg:block">
              <table className="w-full min-w-[820px] text-sm">
                <thead className="bg-muted/60 text-left">
                  <tr>
                    <th className="px-4 py-3">{t("proc.vendors.field.code")}</th>
                    <th className="px-4 py-3">{t("proc.vendors.field.companyName")}</th>
                    <th className="px-4 py-3">{t("proc.vendors.field.type")}</th>
                    <th className="px-4 py-3">{t("proc.vendors.field.contact")}</th>
                    <th className="px-4 py-3">{t("proc.vendors.field.status")}</th>
                    <th className="px-4 py-3 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {rows.map((vendor) => (
                    <tr
                      key={vendor.id}
                      data-testid="vendor-row"
                      role="link"
                      tabIndex={0}
                      aria-label={`${t("common.view")}: ${vendor.companyName}`}
                      className="group cursor-pointer transition-colors hover:bg-muted/60 focus-visible:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                      onClick={() => navigate(`/admin/vendors/${vendor.id}`)}
                      onKeyDown={(event) => {
                        if (event.currentTarget === event.target && event.key === "Enter")
                          navigate(`/admin/vendors/${vendor.id}`);
                      }}
                    >
                      <td className="px-4 py-4 font-semibold group-hover:text-primary group-focus-visible:text-primary">
                        {vendor.vendorCode}
                      </td>
                      <td className="px-4 py-4">
                        <p className="font-semibold group-hover:text-primary group-focus-visible:text-primary">
                          {vendor.companyName}
                        </p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {vendor.tradeCategory || t("common.noData")}
                        </p>
                      </td>
                      <td className="px-4 py-4">{t(`proc.vendors.type.${vendor.vendorType}`)}</td>
                      <td className="px-4 py-4">
                        <p>{vendor.contactPerson || t("common.noData")}</p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {vendor.phone || vendor.email || t("common.noData")}
                        </p>
                      </td>
                      <td className="px-4 py-4">
                        <Badge variant={vendor.isActive ? "default" : "secondary"}>
                          {t(
                            vendor.isActive
                              ? "proc.vendors.status.active"
                              : "proc.vendors.status.inactive",
                          )}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-1">
                          <Button
                            className="h-11 w-11 hover:bg-background hover:shadow-sm"
                            variant="ghost"
                            size="icon"
                            title={t("common.view")}
                            aria-label={`${t("common.view")}: ${vendor.companyName}`}
                            onClick={(event) => {
                              event.stopPropagation();
                              navigate(`/admin/vendors/${vendor.id}`);
                            }}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                          {canManage && (
                            <>
                              <Button
                                className="h-11 w-11 hover:bg-background hover:shadow-sm"
                                variant="ghost"
                                size="icon"
                                title={t("common.edit")}
                                aria-label={`${t("common.edit")}: ${vendor.companyName}`}
                                onClick={(event) => {
                                  event.stopPropagation();
                                  setEditing(vendor);
                                }}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.delete")}
                                aria-label={`${t("common.delete")}: ${vendor.companyName}`}
                                className="h-11 w-11 text-destructive hover:bg-destructive/10 hover:text-destructive"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  void openDelete(vendor);
                                }}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="grid gap-3 lg:hidden">
              {rows.map((vendor) => (
                <article
                  key={vendor.id}
                  data-testid="vendor-card"
                  className="overflow-hidden rounded-xl border bg-card shadow-sm transition hover:border-primary/40 hover:shadow-md focus-within:border-primary/40 focus-within:shadow-md"
                >
                  <button
                    type="button"
                    onClick={() => navigate(`/admin/vendors/${vendor.id}`)}
                    className="group w-full p-4 text-left outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-semibold transition-colors group-hover:text-primary group-focus-visible:text-primary">
                          {vendor.companyName}
                        </p>
                        <p className="mt-1 break-words text-xs text-muted-foreground">
                          {vendor.vendorCode} · {t(`proc.vendors.type.${vendor.vendorType}`)}
                        </p>
                      </div>
                      <Badge
                        className="shrink-0"
                        variant={vendor.isActive ? "default" : "secondary"}
                      >
                        {t(
                          vendor.isActive
                            ? "proc.vendors.status.active"
                            : "proc.vendors.status.inactive",
                        )}
                      </Badge>
                    </div>
                    <div className="mt-4 flex items-end justify-between gap-3">
                      <div className="min-w-0">
                        <p className="text-sm">{vendor.contactPerson || t("common.noData")}</p>
                        <p className="mt-0.5 break-all text-xs text-muted-foreground">
                          {vendor.phone || vendor.email || t("common.noData")}
                        </p>
                      </div>
                      <ArrowRight className="h-5 w-5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-1 group-hover:text-primary group-focus-visible:translate-x-1 group-focus-visible:text-primary" />
                    </div>
                  </button>
                  <div className="flex flex-wrap gap-1 border-t bg-muted/20 p-2">
                    <Button
                      className="min-h-11 min-w-24 flex-1"
                      variant="ghost"
                      size="sm"
                      onClick={() => navigate(`/admin/vendors/${vendor.id}`)}
                    >
                      <Eye className="mr-1.5 h-4 w-4" />
                      {t("common.view")}
                    </Button>
                    {canManage && (
                      <>
                        <Button
                          className="min-h-11 min-w-24 flex-1"
                          variant="ghost"
                          size="sm"
                          onClick={() => setEditing(vendor)}
                        >
                          <Pencil className="mr-1.5 h-4 w-4" />
                          {t("common.edit")}
                        </Button>
                        <Button
                          className="min-h-11 min-w-24 flex-1 text-destructive hover:bg-destructive/10 hover:text-destructive"
                          variant="ghost"
                          size="sm"
                          onClick={() => void openDelete(vendor)}
                        >
                          <Trash2 className="mr-1.5 h-4 w-4" />
                          {t("common.delete")}
                        </Button>
                      </>
                    )}
                  </div>
                </article>
              ))}
            </div>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-muted-foreground">
                {t("proc.vendors.total")} {total}
              </p>
              <div className="flex items-center gap-2">
                <Button
                  className="min-h-11 sm:min-h-9"
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((value) => value - 1)}
                >
                  {t("common.prev")}
                </Button>
                <span className="min-w-10 text-center text-sm">
                  {page} / {pages}
                </span>
                <Button
                  className="min-h-11 sm:min-h-9"
                  variant="outline"
                  size="sm"
                  disabled={page >= pages}
                  onClick={() => setPage((value) => value + 1)}
                >
                  {t("common.next")}
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("proc.vendors.createTitle")}</DialogTitle>
            <DialogDescription>{t("proc.vendors.formDescription")}</DialogDescription>
          </DialogHeader>
          <VendorForm onSubmit={create} onCancel={() => setCreating(false)} />
        </DialogContent>
      </Dialog>
      <Dialog
        open={editing !== null}
        onOpenChange={(open) => {
          if (!open) setEditing(null);
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("proc.vendors.editTitle")}</DialogTitle>
            <DialogDescription>{t("proc.vendors.formDescription")}</DialogDescription>
          </DialogHeader>
          {editing && (
            <VendorForm
              vendor={editing}
              onSubmit={(request) => update(request as UpdateVendorRequest)}
              onCancel={() => setEditing(null)}
            />
          )}
        </DialogContent>
      </Dialog>
      <DeletionImpactDialog
        open={pendingDelete !== null}
        impact={deleteImpact}
        loading={loadingImpact}
        deleting={deleting}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !deleting) setPendingDelete(null);
        }}
        onConfirm={deleteVendor}
        onCompleted={deleteCompleted}
      />
    </AdminLayout>
  );
}
