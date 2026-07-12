import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search, Trash2, RefreshCw, Star, X, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { PageLoading, PageError } from "@/components/PageState";import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  adminApi,
  type CreateCustomerRequest,
  type CustomerActivityType,
  type CustomerContactResponse,
  type CustomerDuplicateDetail,
  type CustomerListParams,
  type CustomerRelationshipStatus,
  type CustomerResponse,
  type CustomerType,
  type MasterDataOption,
  type UpdateCustomerRequest,
  type UpsertCustomerContactRequest,
} from "@/services/adminApi";

const TYPES: CustomerType[] = ["Individual", "Company"];
const STATUSES: CustomerRelationshipStatus[] = ["Prospect", "InProgress", "Signed", "Suspended"];
const ACTIVITY_TYPES: CustomerActivityType[] = ["Call", "Email", "Meeting", "Note"];

const statusBadge = (
  s: CustomerRelationshipStatus,
): "default" | "secondary" | "outline" | "destructive" => {
  switch (s) {
    case "Signed":
      return "default";
    case "Suspended":
      return "destructive";
    case "InProgress":
      return "secondary";
    default:
      return "outline";
  }
};

const emptyContact: UpsertCustomerContactRequest = {
  fullName: "",
  position: "",
  phone: "",
  email: "",
  isPrimary: true,
};

const emptyCreate: CreateCustomerRequest = {
  type: "Individual",
  name: "",
  sourceCode: "",
  primaryContact: { ...emptyContact },
};

/**
 * Extracts a user-facing error message from an axios error. Handles three
 * shapes the backend returns:
 *   - 400 ModelState  → { errors: { "Field": ["msg"] } }
 *   - 400 service     → { message: "…" }
 *   - fallback        → err.message ("Request failed with status code 400")
 */
const extractApiError = (err: unknown): string => {
  const anyErr = err as {
    response?: {
      data?: unknown;
    };
    message?: string;
  };
  const data = anyErr.response?.data;
  if (data && typeof data === "object") {
    if ("errors" in data && data.errors && typeof data.errors === "object") {
      return Object.entries(data.errors as Record<string, string[]>)
        .map(([k, v]) => `${k}: ${v.join("; ")}`)
        .join(" · ");
    }
    if ("message" in data && typeof data.message === "string") {
      return data.message;
    }
  }
  return anyErr.message ?? String(err);
};

const AdminCustomers = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canSeeAll = has(ADMIN_PERMS.customersViewAll);
  const canManage = has(ADMIN_PERMS.customersManage);

  const [rows, setRows] = useState<CustomerResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [sources, setSources] = useState<MasterDataOption[]>([]);

  const [typeFilter, setTypeFilter] = useState<CustomerType | "">("");
  const [statusFilter, setStatusFilter] = useState<CustomerRelationshipStatus | "">("");
  const [sourceFilter, setSourceFilter] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [createForm, setCreateForm] = useState<CreateCustomerRequest>(emptyCreate);
  const [createError, setCreateError] = useState<string | null>(null);
  const [duplicate, setDuplicate] = useState<CustomerDuplicateDetail | null>(null);
  const [overrideReason, setOverrideReason] = useState("");

  const [detail, setDetail] = useState<CustomerResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editForm, setEditForm] = useState<UpdateCustomerRequest | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);

  const [contactForm, setContactForm] = useState<UpsertCustomerContactRequest | null>(null);
  const [savingContact, setSavingContact] = useState(false);

  const [activityType, setActivityType] = useState<CustomerActivityType>("Call");
  const [activityContent, setActivityContent] = useState("");
  const [addingActivity, setAddingActivity] = useState(false);

  useEffect(() => {
    const h = window.setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 350);
    return () => window.clearTimeout(h);
  }, [searchInput]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: CustomerListParams = { page, pageSize };
      if (typeFilter) params.type = typeFilter;
      if (statusFilter) params.status = statusFilter;
      if (sourceFilter) params.sourceCode = sourceFilter;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listCustomers(params);
      setRows(data.items);
      setTotal(data.total);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, [page, typeFilter, statusFilter, sourceFilter, search]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("customer_source");
        if (!cancelled) setSources(data);
      } catch {
        // best-effort — dropdown may just be empty
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const sourceLabelByCode = useMemo(() => {
    const map = new Map<string, string>();
    sources.forEach((s) => map.set(s.code, s.name));
    return map;
  }, [sources]);

  const openDetail = async (id: number, options: { startEditing?: boolean } = {}) => {
    setDetailLoading(true);
    setDetail(null);
    setEditing(false);
    setEditForm(null);
    try {
      const { data } = await adminApi.getCustomer(id);
      setDetail(data);
      if (options.startEditing && canManage) {
        setEditForm({
          type: data.type,
          name: data.name,
          taxId: data.taxId,
          address: data.address,
          representativeName: data.representativeName,
          sourceCode: data.sourceCode,
          relationshipStatus: data.relationshipStatus,
          ownerUserId: data.ownerUserId,
          note: data.note,
        });
        setEditing(true);
      }
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetail = () => {
    setDetail(null);
    setEditing(false);
    setEditForm(null);
    setContactForm(null);
    setActivityContent("");
  };

  const handleSaveCreate = async (opts?: { overrideReason?: string }) => {
    setCreateError(null);
    setDuplicate(null);

    const type = createForm.type;
    if (!createForm.name.trim() || !createForm.sourceCode) {
      setCreateError(t("customers.validation.missingFields"));
      return;
    }
    if (type === "Company" &&
        (!createForm.taxId?.trim() || !createForm.address?.trim() || !createForm.representativeName?.trim())) {
      setCreateError(t("customers.validation.companyFieldsMissing"));
      return;
    }
    if (!createForm.primaryContact.fullName?.trim() ||
        (!createForm.primaryContact.phone?.trim() && !createForm.primaryContact.email?.trim())) {
      setCreateError(t("customers.validation.primaryContact"));
      return;
    }

    setSaving(true);
    try {
      // Strip empty strings so [EmailAddress] validation doesn't reject "".
      const primary = createForm.primaryContact;
      await adminApi.createCustomer({
        ...createForm,
        taxId: createForm.taxId?.trim() || undefined,
        address: createForm.address?.trim() || undefined,
        representativeName: createForm.representativeName?.trim() || undefined,
        note: createForm.note?.trim() || undefined,
        primaryContact: {
          fullName: primary.fullName.trim(),
          position: primary.position?.trim() || undefined,
          phone: primary.phone?.trim() || undefined,
          email: primary.email?.trim() || undefined,
          isPrimary: true,
        },
        duplicateOverrideReason: opts?.overrideReason,
      });
      toast({ title: t("customers.created") });
      setCreating(false);
      setCreateForm(emptyCreate);
      setOverrideReason("");
      await fetchList();
    } catch (err) {
      const anyErr = err as { response?: { status?: number; data?: CustomerDuplicateDetail } };
      if (anyErr.response?.status === 409 && anyErr.response.data && "field" in anyErr.response.data) {
        setDuplicate(anyErr.response.data);
      } else {
        setCreateError(extractApiError(err));
      }
    } finally {
      setSaving(false);
    }
  };

  const handleSaveEdit = async () => {
    if (!detail || !editForm) return;
    setSavingEdit(true);
    try {
      const { data } = await adminApi.updateCustomer(detail.id, {
        ...editForm,
        taxId: editForm.taxId?.trim() || undefined,
        address: editForm.address?.trim() || undefined,
        representativeName: editForm.representativeName?.trim() || undefined,
        note: editForm.note?.trim() || undefined,
      });
      setDetail(data);
      setEditing(false);
      toast({ title: t("customers.updated") });
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setSavingEdit(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm(t("customers.deleteConfirm"))) return;
    try {
      await adminApi.deleteCustomer(id);
      toast({ title: t("customers.deleted") });
      if (detail?.id === id) closeDetail();
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const handleSaveContact = async () => {
    if (!detail || !contactForm) return;
    if (!contactForm.fullName.trim() ||
        (!contactForm.phone?.trim() && !contactForm.email?.trim())) {
      toast({ title: t("customers.validation.primaryContact"), variant: "destructive" });
      return;
    }
    setSavingContact(true);
    try {
      await adminApi.upsertCustomerContact(detail.id, {
        id: contactForm.id,
        fullName: contactForm.fullName.trim(),
        position: contactForm.position?.trim() || undefined,
        phone: contactForm.phone?.trim() || undefined,
        email: contactForm.email?.trim() || undefined,
        isPrimary: contactForm.isPrimary,
      });
      const { data } = await adminApi.getCustomer(detail.id);
      setDetail(data);
      setContactForm(null);
      toast({ title: t("customers.updated") });
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setSavingContact(false);
    }
  };

  const handleDeleteContact = async (contact: CustomerContactResponse) => {
    if (!detail) return;
    if (detail.contacts.length <= 1) {
      toast({ title: t("customers.contact.lastOneCannotDelete"), variant: "destructive" });
      return;
    }
    try {
      await adminApi.deleteCustomerContact(detail.id, contact.id);
      const { data } = await adminApi.getCustomer(detail.id);
      setDetail(data);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const handleAddActivity = async () => {
    if (!detail || !activityContent.trim()) return;
    setAddingActivity(true);
    try {
      await adminApi.addCustomerActivity(detail.id, {
        type: activityType,
        content: activityContent.trim(),
      });
      const { data } = await adminApi.getCustomer(detail.id);
      setDetail(data);
      setActivityContent("");
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setAddingActivity(false);
    }
  };

  const primaryContact = (c: CustomerResponse) =>
    c.contacts.find((x) => x.isPrimary) ?? c.contacts[0];

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("customers.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("customers.subtitle")}</p>
            <p className="text-xs text-muted-foreground italic mt-1">
              {(canSeeAll ? t("customers.totalCount") : t("customers.myScopeCount")).replace(
                "{count}",
                total.toString(),
              )}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCw className="mr-1.5 h-4 w-4" /> {t("common.refresh")}
            </Button>
            {canManage && (
              <Button onClick={() => setCreating(true)}>
                <Plus className="mr-1.5 h-4 w-4" /> {t("customers.new")}
              </Button>
            )}
          </div>
        </header>

        <section className="flex flex-wrap items-end gap-2 rounded-lg border bg-card p-3">
          <div className="relative min-w-[220px] flex-1">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t("customers.searchPlaceholder")}
              className="pl-9"
              aria-label={t("customers.searchPlaceholder")}
            />
          </div>
          <div className="w-[150px]">
            <Label className="text-xs">{t("customers.filter.type")}</Label>
            <Select
              value={typeFilter || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setTypeFilter(v === "__all" ? "" : (v as CustomerType));
              }}
            >
              <SelectTrigger className="h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("customers.filter.all")}</SelectItem>
                {TYPES.map((tp) => (
                  <SelectItem key={tp} value={tp}>{t(`customers.type.${tp}`)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="w-[170px]">
            <Label className="text-xs">{t("customers.filter.status")}</Label>
            <Select
              value={statusFilter || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setStatusFilter(v === "__all" ? "" : (v as CustomerRelationshipStatus));
              }}
            >
              <SelectTrigger className="h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("customers.filter.all")}</SelectItem>
                {STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>{t(`customers.status.${s}`)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="w-[170px]">
            <Label className="text-xs">{t("customers.filter.source")}</Label>
            <Select
              value={sourceFilter || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setSourceFilter(v === "__all" ? "" : v);
              }}
            >
              <SelectTrigger className="h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("customers.filter.all")}</SelectItem>
                {sources.map((s) => (
                  <SelectItem key={s.code} value={s.code}>{s.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            {t("customers.empty")}
          </div>
        ) : (
          <div className="overflow-x-auto rounded-lg border">
            <table className="min-w-full divide-y text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.type")}</th>
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.name")}</th>
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.primaryContact")}</th>
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.source")}</th>
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.status")}</th>
                  {canSeeAll && (
                    <th className="px-3 py-2 text-left font-medium">{t("customers.field.owner")}</th>
                  )}
                  <th className="px-3 py-2 text-left font-medium">{t("customers.field.createdAt")}</th>
                  {canManage && <th className="px-3 py-2" />}
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((c) => {
                  const primary = primaryContact(c);
                  return (
                    <tr
                      key={c.id}
                      className="cursor-pointer hover:bg-muted/40"
                      onClick={() => void openDetail(c.id)}
                    >
                      <td className="px-3 py-2">
                        <Badge variant="outline">{t(`customers.type.${c.type}`)}</Badge>
                      </td>
                      <td className="px-3 py-2 font-medium">
                        {c.name}
                        {c.type === "Company" && c.taxId && (
                          <div className="text-xs text-muted-foreground">MST: {c.taxId}</div>
                        )}
                      </td>
                      <td className="px-3 py-2 text-xs">
                        {primary ? (
                          <>
                            <div>{primary.fullName}</div>
                            <div className="text-muted-foreground">{primary.phone || primary.email || "—"}</div>
                          </>
                        ) : "—"}
                      </td>
                      <td className="px-3 py-2 text-xs">
                        {sourceLabelByCode.get(c.sourceCode) ?? c.sourceCode}
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant={statusBadge(c.relationshipStatus)}>
                          {t(`customers.status.${c.relationshipStatus}`)}
                        </Badge>
                      </td>
                      {canSeeAll && (
                        <td className="px-3 py-2 text-xs text-muted-foreground">{c.ownerName || "—"}</td>
                      )}
                      <td className="px-3 py-2 text-xs text-muted-foreground">
                        {new Date(c.createdAt).toLocaleString()}
                      </td>
                      {canManage && (
                        <td className="px-3 py-2 text-right">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={(e) => {
                                e.stopPropagation();
                                void openDetail(c.id, { startEditing: true });
                              }}
                              title={t("common.edit")}
                              aria-label={t("common.edit")}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={(e) => {
                                e.stopPropagation();
                                void handleDelete(c.id);
                              }}
                              title={t("common.delete")}
                              aria-label={t("common.delete")}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {total > pageSize && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{(page - 1) * pageSize + 1}-{Math.min(page * pageSize, total)} / {total}</span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}>‹</Button>
              <Button variant="outline" size="sm" onClick={() => setPage((p) => p + 1)} disabled={page * pageSize >= total}>›</Button>
            </div>
          </div>
        )}
      </div>

      {/* Create dialog */}
      <Dialog
        open={creating}
        onOpenChange={(o) => {
          if (!o) {
            setCreating(false);
            setCreateForm(emptyCreate);
            setCreateError(null);
          }
        }}
      >
        <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("customers.new")}</DialogTitle>
            <DialogDescription>{t("customers.validation.primaryContact")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("customers.field.type")} *</Label>
                <Select
                  value={createForm.type}
                  onValueChange={(v) => setCreateForm({ ...createForm, type: v as CustomerType })}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {TYPES.map((tp) => (
                      <SelectItem key={tp} value={tp}>{t(`customers.type.${tp}`)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>{t("customers.field.source")} *</Label>
                <Select
                  value={createForm.sourceCode || undefined}
                  onValueChange={(v) => setCreateForm({ ...createForm, sourceCode: v })}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {sources.map((s) => (
                      <SelectItem key={s.code} value={s.code}>{s.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label>{t("customers.field.name")} *</Label>
              <Input
                value={createForm.name}
                onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                autoFocus
              />
            </div>
            {createForm.type === "Company" && (
              <>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <Label>{t("customers.field.taxId")} *</Label>
                    <Input
                      value={createForm.taxId ?? ""}
                      onChange={(e) => setCreateForm({ ...createForm, taxId: e.target.value })}
                    />
                  </div>
                  <div>
                    <Label>{t("customers.field.representativeName")} *</Label>
                    <Input
                      value={createForm.representativeName ?? ""}
                      onChange={(e) => setCreateForm({ ...createForm, representativeName: e.target.value })}
                    />
                  </div>
                </div>
                <div>
                  <Label>{t("customers.field.address")} *</Label>
                  <Textarea
                    rows={2}
                    value={createForm.address ?? ""}
                    onChange={(e) => setCreateForm({ ...createForm, address: e.target.value })}
                  />
                </div>
              </>
            )}
            <div className="rounded border p-3 space-y-3">
              <h4 className="text-sm font-medium">{t("customers.field.primaryContact")} *</h4>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("customers.contact.fullName")} *</Label>
                  <Input
                    value={createForm.primaryContact.fullName}
                    onChange={(e) => setCreateForm({
                      ...createForm,
                      primaryContact: { ...createForm.primaryContact, fullName: e.target.value },
                    })}
                  />
                </div>
                <div>
                  <Label>{t("customers.contact.position")}</Label>
                  <Input
                    value={createForm.primaryContact.position ?? ""}
                    onChange={(e) => setCreateForm({
                      ...createForm,
                      primaryContact: { ...createForm.primaryContact, position: e.target.value },
                    })}
                  />
                </div>
                <div>
                  <Label>{t("customers.contact.phone")}</Label>
                  <Input
                    value={createForm.primaryContact.phone ?? ""}
                    onChange={(e) => setCreateForm({
                      ...createForm,
                      primaryContact: { ...createForm.primaryContact, phone: e.target.value },
                    })}
                  />
                </div>
                <div>
                  <Label>{t("customers.contact.email")}</Label>
                  <Input
                    type="email"
                    value={createForm.primaryContact.email ?? ""}
                    onChange={(e) => setCreateForm({
                      ...createForm,
                      primaryContact: { ...createForm.primaryContact, email: e.target.value },
                    })}
                  />
                </div>
              </div>
            </div>
            <div>
              <Label>{t("customers.field.note")}</Label>
              <Textarea
                rows={2}
                value={createForm.note ?? ""}
                onChange={(e) => setCreateForm({ ...createForm, note: e.target.value })}
              />
            </div>
            {createError && (
              <p className="rounded bg-destructive/10 px-3 py-2 text-xs text-destructive">{createError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void handleSaveCreate()} disabled={saving}>
              {saving ? "…" : t("customers.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Duplicate override dialog */}
      <Dialog
        open={!!duplicate}
        onOpenChange={(o) => {
          if (!o) {
            setDuplicate(null);
            setOverrideReason("");
          }
        }}
      >
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{t("customers.duplicate.title")}</DialogTitle>
            <DialogDescription>
              {duplicate ? t("customers.duplicate.body")
                .replace("{name}", duplicate.existingCustomerName)
                .replace("{id}", String(duplicate.existingCustomerId))
                .replace("{field}", duplicate.field)
                .replace("{value}", duplicate.value)
                : ""}
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={overrideReason}
            onChange={(e) => setOverrideReason(e.target.value)}
            placeholder={t("customers.duplicate.reasonPlaceholder")}
            rows={3}
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => {
                setDuplicate(null);
                setOverrideReason("");
              }}
              disabled={saving}
            >
              {t("common.cancel")}
            </Button>
            <Button
              onClick={() => void handleSaveCreate({ overrideReason })}
              disabled={saving || !overrideReason.trim()}
            >
              {saving ? "…" : t("customers.duplicate.saveAnyway")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Detail dialog */}
      <Dialog open={!!detail || detailLoading} onOpenChange={(o) => !o && closeDetail()}>
        <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
          {detailLoading ? (
            <>
              <DialogHeader>
                <DialogTitle className="sr-only">{t("customers.title")}</DialogTitle>
                <DialogDescription className="sr-only">{t("common.loading")}</DialogDescription>
              </DialogHeader>
              <PageLoading />
            </>
          ) : detail ? (
            <>
              <DialogHeader>
                <DialogTitle className="flex items-center gap-2">
                  {detail.name}
                  <Badge variant="outline">{t(`customers.type.${detail.type}`)}</Badge>
                  <Badge variant={statusBadge(detail.relationshipStatus)}>
                    {t(`customers.status.${detail.relationshipStatus}`)}
                  </Badge>
                </DialogTitle>
                <DialogDescription>
                  {sourceLabelByCode.get(detail.sourceCode) ?? detail.sourceCode} · {detail.ownerName ?? `#${detail.ownerUserId ?? "—"}`}
                </DialogDescription>
              </DialogHeader>

              <Tabs defaultValue="general">
                <TabsList className="w-full flex-wrap justify-start">
                  <TabsTrigger value="general">{t("customers.tab.general")}</TabsTrigger>
                  <TabsTrigger value="contacts">
                    {t("customers.tab.contacts")} ({detail.contacts.length})
                  </TabsTrigger>
                  <TabsTrigger value="related">{t("customers.tab.related")}</TabsTrigger>
                  <TabsTrigger value="timeline">
                    {t("customers.tab.timeline")} ({detail.activities.length})
                  </TabsTrigger>
                </TabsList>

                <TabsContent value="general" className="space-y-3 pt-3">
                  {!editing ? (
                    <>
                      <div className="grid grid-cols-2 gap-4 text-sm">
                        {detail.type === "Company" && (
                          <>
                            <div>
                              <div className="text-xs text-muted-foreground">{t("customers.field.taxId")}</div>
                              <div>{detail.taxId || "—"}</div>
                            </div>
                            <div>
                              <div className="text-xs text-muted-foreground">{t("customers.field.representativeName")}</div>
                              <div>{detail.representativeName || "—"}</div>
                            </div>
                            <div className="col-span-2">
                              <div className="text-xs text-muted-foreground">{t("customers.field.address")}</div>
                              <div className="whitespace-pre-wrap">{detail.address || "—"}</div>
                            </div>
                          </>
                        )}
                        <div>
                          <div className="text-xs text-muted-foreground">{t("customers.field.createdAt")}</div>
                          <div>{new Date(detail.createdAt).toLocaleString()}</div>
                        </div>
                        {detail.note && (
                          <div className="col-span-2">
                            <div className="text-xs text-muted-foreground">{t("customers.field.note")}</div>
                            <div className="whitespace-pre-wrap">{detail.note}</div>
                          </div>
                        )}
                      </div>
                    </>
                  ) : editForm && (
                    <div className="space-y-3">
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <Label>{t("customers.field.type")}</Label>
                          <Select
                            value={editForm.type}
                            onValueChange={(v) => setEditForm({ ...editForm, type: v as CustomerType })}
                          >
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              {TYPES.map((tp) => (
                                <SelectItem key={tp} value={tp}>{t(`customers.type.${tp}`)}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <div>
                          <Label>{t("customers.field.status")}</Label>
                          <Select
                            value={editForm.relationshipStatus}
                            onValueChange={(v) => setEditForm({
                              ...editForm,
                              relationshipStatus: v as CustomerRelationshipStatus,
                            })}
                          >
                            <SelectTrigger><SelectValue /></SelectTrigger>
                            <SelectContent>
                              {STATUSES.map((s) => (
                                <SelectItem key={s} value={s}>{t(`customers.status.${s}`)}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                      </div>
                      <div>
                        <Label>{t("customers.field.name")}</Label>
                        <Input
                          value={editForm.name}
                          onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                        />
                      </div>
                      {editForm.type === "Company" && (
                        <>
                          <div className="grid grid-cols-2 gap-3">
                            <div>
                              <Label>{t("customers.field.taxId")}</Label>
                              <Input
                                value={editForm.taxId ?? ""}
                                onChange={(e) => setEditForm({ ...editForm, taxId: e.target.value })}
                              />
                            </div>
                            <div>
                              <Label>{t("customers.field.representativeName")}</Label>
                              <Input
                                value={editForm.representativeName ?? ""}
                                onChange={(e) => setEditForm({ ...editForm, representativeName: e.target.value })}
                              />
                            </div>
                          </div>
                          <div>
                            <Label>{t("customers.field.address")}</Label>
                            <Textarea
                              rows={2}
                              value={editForm.address ?? ""}
                              onChange={(e) => setEditForm({ ...editForm, address: e.target.value })}
                            />
                          </div>
                        </>
                      )}
                      <div>
                        <Label>{t("customers.field.source")}</Label>
                        <Select
                          value={editForm.sourceCode}
                          onValueChange={(v) => setEditForm({ ...editForm, sourceCode: v })}
                        >
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {sources.map((s) => (
                              <SelectItem key={s.code} value={s.code}>{s.name}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div>
                        <Label>{t("customers.field.note")}</Label>
                        <Textarea
                          rows={2}
                          value={editForm.note ?? ""}
                          onChange={(e) => setEditForm({ ...editForm, note: e.target.value })}
                        />
                      </div>
                      <div className="flex gap-2">
                        <Button size="sm" onClick={() => void handleSaveEdit()} disabled={savingEdit}>
                          {savingEdit ? "…" : t("customers.save")}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            setEditing(false);
                            setEditForm(null);
                          }}
                        >
                          {t("common.cancel")}
                        </Button>
                      </div>
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="contacts" className="space-y-2 pt-3">
                  <ul className="space-y-2">
                    {detail.contacts.map((c) => (
                      <li key={c.id} className="flex items-start justify-between rounded border p-2 text-sm">
                        <div>
                          <div className="flex items-center gap-2 font-medium">
                            {c.fullName}
                            {c.isPrimary && (
                              <Badge variant="default" className="text-[10px]">
                                <Star className="mr-1 h-3 w-3" />{t("customers.contact.primaryBadge")}
                              </Badge>
                            )}
                          </div>
                          {c.position && (
                            <div className="text-xs text-muted-foreground">{c.position}</div>
                          )}
                          <div className="text-xs">
                            {c.phone || "—"} · {c.email || "—"}
                          </div>
                        </div>
                        {canManage && (
                          <div className="flex gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={() => setContactForm({ ...c })}
                              title={t("customers.contact.edit")}
                              aria-label={t("customers.contact.edit")}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              disabled={detail.contacts.length <= 1}
                              onClick={() => void handleDeleteContact(c)}
                              title={t("common.delete")}
                              aria-label={t("common.delete")}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        )}
                      </li>
                    ))}
                  </ul>
                  {canManage && !contactForm && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setContactForm({ ...emptyContact, isPrimary: false })}
                    >
                      <Plus className="mr-1.5 h-4 w-4" /> {t("customers.contact.add")}
                    </Button>
                  )}
                  {contactForm && (
                    <div className="rounded border p-3 space-y-2">
                      <div className="flex items-center justify-between">
                        <h4 className="text-sm font-medium">
                          {contactForm.id ? t("customers.contact.edit") : t("customers.contact.add")}
                        </h4>
                        <Button
                          variant="ghost"
                          size="icon"
                          onClick={() => setContactForm(null)}
                          aria-label={t("common.close")}
                        >
                          <X className="h-4 w-4" />
                        </Button>
                      </div>
                      <div className="grid grid-cols-2 gap-2">
                        <div>
                          <Label className="text-xs">{t("customers.contact.fullName")} *</Label>
                          <Input
                            value={contactForm.fullName}
                            onChange={(e) => setContactForm({ ...contactForm, fullName: e.target.value })}
                          />
                        </div>
                        <div>
                          <Label className="text-xs">{t("customers.contact.position")}</Label>
                          <Input
                            value={contactForm.position ?? ""}
                            onChange={(e) => setContactForm({ ...contactForm, position: e.target.value })}
                          />
                        </div>
                        <div>
                          <Label className="text-xs">{t("customers.contact.phone")}</Label>
                          <Input
                            value={contactForm.phone ?? ""}
                            onChange={(e) => setContactForm({ ...contactForm, phone: e.target.value })}
                          />
                        </div>
                        <div>
                          <Label className="text-xs">{t("customers.contact.email")}</Label>
                          <Input
                            type="email"
                            value={contactForm.email ?? ""}
                            onChange={(e) => setContactForm({ ...contactForm, email: e.target.value })}
                          />
                        </div>
                      </div>
                      <label className="flex items-center gap-2 text-xs">
                        <Checkbox
                          checked={!!contactForm.isPrimary}
                          onCheckedChange={(v) => setContactForm({ ...contactForm, isPrimary: v === true })}
                        />
                        {t("customers.contact.isPrimary")}
                      </label>
                      <Button size="sm" onClick={() => void handleSaveContact()} disabled={savingContact}>
                        {savingContact ? "…" : t("customers.save")}
                      </Button>
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="related" className="pt-3">
                  <p className="rounded border border-dashed p-4 text-center text-xs text-muted-foreground">
                    {t("customers.tab.related.empty")}
                  </p>
                </TabsContent>

                <TabsContent value="timeline" className="space-y-3 pt-3">
                  {detail.activities.length === 0 ? (
                    <p className="rounded border border-dashed p-4 text-center text-xs text-muted-foreground">
                      {t("customers.activity.empty")}
                    </p>
                  ) : (
                    <ol className="space-y-2 border-l pl-4">
                      {detail.activities.map((a) => (
                        <li key={a.id} className="relative">
                          <span className="absolute -left-[19px] top-1 h-2.5 w-2.5 rounded-full bg-primary" />
                          <div className="text-xs text-muted-foreground">
                            {t(`customers.activity.${a.type}`)} · {new Date(a.occurredAt).toLocaleString()}
                            {a.createdByName ? ` · ${a.createdByName}` : ""}
                          </div>
                          <div className="whitespace-pre-wrap text-sm">{a.content}</div>
                        </li>
                      ))}
                    </ol>
                  )}
                  {detail.relationshipStatus !== "Suspended" && canManage && (
                    <div className="flex flex-col gap-2 rounded border p-3">
                      <div className="flex flex-col gap-2 sm:flex-row">
                        <Select
                          value={activityType}
                          onValueChange={(v) => setActivityType(v as CustomerActivityType)}
                        >
                          <SelectTrigger className="w-full sm:w-[140px]"><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {ACTIVITY_TYPES.map((tp) => (
                              <SelectItem key={tp} value={tp}>{t(`customers.activity.${tp}`)}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <Textarea
                          value={activityContent}
                          onChange={(e) => setActivityContent(e.target.value)}
                          placeholder={t("customers.activity.title")}
                          rows={2}
                          className="flex-1"
                        />
                      </div>
                      <Button
                        size="sm"
                        onClick={() => void handleAddActivity()}
                        disabled={!activityContent.trim() || addingActivity}
                      >
                        {addingActivity ? "…" : t("customers.activity.add")}
                      </Button>
                    </div>
                  )}
                </TabsContent>
              </Tabs>

              <DialogFooter>
                <Button variant="outline" onClick={closeDetail}>
                  {t("common.close")}
                </Button>
              </DialogFooter>
            </>
          ) : null}
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AdminCustomers;
