import { useCallback, useEffect, useMemo, useState } from "react";
import { Edit, Plus, RefreshCw, Search, Trash2, UserRound } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
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
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type CreateUserRequest,
  type UpdateUserRequest,
  type UserDetailResponse,
  type UserListItemResponse,
} from "@/services/adminApi";
import { rbacApi, type RoleResponse } from "@/services/rbacApi";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { newIdempotencyKey } from "@/lib/api";
import UserFormModal from "./UserFormModal";

const PAGE_SIZE = 20;

const ROLE_STYLES: Record<string, string> = {
  SUPER_ADMIN: "border-rose-200 bg-rose-50 text-rose-700",
  ADMIN: "border-indigo-200 bg-indigo-50 text-indigo-700",
  USER: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

const getErrorMessage = (error: unknown) => {
  if (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof error.response === "object" &&
    error.response !== null &&
    "data" in error.response &&
    typeof error.response.data === "object" &&
    error.response.data !== null
  ) {
    const data = error.response.data as { detail?: unknown; message?: unknown };
    if (typeof data.detail === "string") return data.detail;
    if (typeof data.message === "string") return data.message;
  }

  return undefined;
};

export default function UserList() {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<UserListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  // Full RBAC catalog (system + custom). Inactive roles are filtered out below
  // before being handed to the form so the dropdown only shows assignable roles.
  const [allRoles, setAllRoles] = useState<RoleResponse[]>([]);
  const [searchText, setSearchText] = useState("");
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<UserDetailResponse | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [busyUserId, setBusyUserId] = useState<number | null>(null);

  const roles = useMemo(() => allRoles.filter((item) => item.isActive), [allRoles]);
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));

  // Build a code -> display label map for the user-list role chip. Prefer the
  // backend-supplied translation key (if it resolves) over the raw name, so
  // localized labels still work for the seeded system roles.
  const roleLabelMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const role of allRoles) {
      const translated = role.labelKey ? t(role.labelKey) : undefined;
      map.set(role.code, translated && translated !== role.labelKey ? translated : role.name);
    }
    return map;
  }, [allRoles, t]);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [usersRes, rolesRes] = await Promise.all([
        adminApi.getUsers({
          skip: (page - 1) * PAGE_SIZE,
          take: PAGE_SIZE,
          search: search || undefined,
          role: role || undefined,
        }),
        rbacApi.listRoles(),
      ]);
      setItems(usersRes.data.items);
      setTotal(usersRes.data.total);
      setAllRoles(rolesRes.data);
    } catch (err) {
      setError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setLoading(false);
    }
  }, [page, role, search, t]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const openCreate = () => {
    setEditingUser(null);
    setModalOpen(true);
  };

  const openEdit = async (user: UserListItemResponse) => {
    setBusyUserId(user.id);
    try {
      const { data } = await adminApi.getUser(user.id);
      setEditingUser(data);
      setModalOpen(true);
    } catch (err) {
      toast({ title: t("common.error"), description: getErrorMessage(err), variant: "destructive" });
    } finally {
      setBusyUserId(null);
    }
  };

  const submitUser = async (payload: CreateUserRequest | UpdateUserRequest) => {
    setSubmitting(true);
    try {
      const idempotencyKey = newIdempotencyKey();
      if (editingUser) {
        await adminApi.updateUser(editingUser.id, payload as UpdateUserRequest, idempotencyKey);
        toast({ title: t("form.updated") });
      } else {
        await adminApi.createUser(payload as CreateUserRequest, idempotencyKey);
        toast({ title: t("form.created") });
      }
      setModalOpen(false);
      setEditingUser(null);
      await loadData();
    } catch (err) {
      toast({ title: t("common.error"), description: getErrorMessage(err), variant: "destructive" });
    } finally {
      setSubmitting(false);
    }
  };

  const toggleActive = async (user: UserListItemResponse) => {
    setBusyUserId(user.id);
    try {
      await adminApi.toggleUserActive(user.id);
      toast({ title: t("form.updated") });
      await loadData();
    } catch (err) {
      toast({ title: t("common.error"), description: getErrorMessage(err), variant: "destructive" });
    } finally {
      setBusyUserId(null);
    }
  };

  const deleteUser = async (user: UserListItemResponse) => {
    if (!window.confirm(t("adminUsers.confirmDelete"))) return;

    setBusyUserId(user.id);
    try {
      await adminApi.deleteUser(user.id);
      toast({ title: t("form.deleted") });
      await loadData();
    } catch (err) {
      toast({ title: t("common.error"), description: getErrorMessage(err), variant: "destructive" });
    } finally {
      setBusyUserId(null);
    }
  };

  const visibleIds = useMemo(() => items.map((u) => u.id), [items]);
  const {
    selectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  } = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteUser(id),
    onAfter: async () => {
      await loadData();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [page, role, search, clearSelection]);

  const applySearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPage(1);
    setSearch(searchText.trim());
  };

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("adminUsers.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {total} {t("adminUsers.totalUsers")}
            </p>
          </div>
          <Button onClick={openCreate}>
            <Plus className="mr-1.5 h-4 w-4" /> {t("adminUsers.addUser")}
          </Button>
        </header>

        <form onSubmit={applySearch} className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
          <div className="min-w-[220px] flex-1">
            <Label className="text-xs" htmlFor="user-search">{t("common.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="user-search"
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                placeholder={t("adminUsers.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          <div className="w-full sm:w-[220px]">
            <Label className="text-xs" htmlFor="user-role">{t("adminUsers.role")}</Label>
            <Select
              value={role || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setRole(v === "__all" ? "" : v);
              }}
            >
              <SelectTrigger id="user-role" className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("adminUsers.allRoles")}</SelectItem>
                {roles.map((item) => (
                  <SelectItem key={item.code} value={item.code}>
                    {roleLabelMap.get(item.code) ?? item.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button type="submit" className="h-9">
            <Search className="mr-1.5 h-4 w-4" /> {t("common.search")}
          </Button>
        </form>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={loadData} />
        ) : (
          <div className="space-y-2">
            <BulkActionBar
              selectedCount={selectedIds.size}
              bulkDeleting={bulkDeleting}
              onClear={clearSelection}
              onBulkDelete={() => void handleBulkDelete()}
            />
            <div className="overflow-x-auto rounded-lg border">
              <table className="min-w-[900px] w-full divide-y text-sm">
                <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="w-10 px-3 py-3 text-left">
                      <Checkbox
                        checked={
                          allVisibleSelected
                            ? true
                            : someVisibleSelected
                              ? "indeterminate"
                              : false
                        }
                        onCheckedChange={(v) => toggleAllVisible(v === true)}
                        aria-label={t("common.selectAll")}
                      />
                    </th>
                    <th className="px-3 py-3 text-left font-medium">{t("adminUsers.user")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("adminUsers.phoneNumber")}</th>
                    <th className="px-3 py-3 text-left font-medium">{t("adminUsers.email")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("adminUsers.role")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("common.status")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {items.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="px-5 py-12 text-center text-muted-foreground">
                        {t("adminUsers.empty")}
                      </td>
                    </tr>
                  ) : (
                    items.map((user) => {
                      const disabled = busyUserId === user.id;

                      return (
                        <tr key={user.id} className="align-middle hover:bg-muted/40 transition">
                          <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selectedIds.has(user.id)}
                              onCheckedChange={(v) => toggleOne(user.id, v === true)}
                              aria-label={`${t("common.selectAll")} · ${user.fullName ?? user.phoneNumber}`}
                            />
                          </td>
                          <td className="px-3 py-3">
                            <div className="flex items-center gap-3">
                              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-semibold text-primary">
                                {user.avatarUrl ? (
                                  <img src={user.avatarUrl} alt="" className="h-full w-full rounded-full object-cover" />
                                ) : (
                                  user.fullName?.[0]?.toUpperCase() ?? <UserRound className="h-4 w-4" />
                                )}
                              </div>
                              <div className="min-w-0">
                                <p className="truncate font-medium">{user.fullName ?? t("adminUsers.unnamed")}</p>
                                <p className="text-xs text-muted-foreground">ID #{user.id}</p>
                              </div>
                            </div>
                          </td>
                          <td className="whitespace-nowrap px-3 py-3 font-medium">{user.phoneNumber}</td>
                          <td className="px-3 py-3">{user.email ?? "—"}</td>
                          <td className="whitespace-nowrap px-3 py-3">
                            <Badge
                              variant="outline"
                              className={cn(
                                "whitespace-nowrap font-medium",
                                ROLE_STYLES[user.role] ?? ROLE_STYLES.USER,
                              )}
                            >
                              {roleLabelMap.get(user.role) ?? user.role}
                            </Badge>
                          </td>
                          <td className="whitespace-nowrap px-3 py-3">
                            <div className="flex items-center gap-2">
                              <Switch
                                checked={user.isActive}
                                onCheckedChange={() => toggleActive(user)}
                                disabled={disabled}
                                aria-label={t("adminUsers.active")}
                              />
                              <span className="text-xs text-muted-foreground">
                                {user.isActive ? t("adminUsers.active") : t("adminUsers.inactive")}
                              </span>
                            </div>
                          </td>
                          <td className="whitespace-nowrap px-3 py-3 text-right">
                            <div className="flex items-center justify-end gap-1">
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                onClick={() => openEdit(user)}
                                disabled={disabled}
                                title={t("common.edit")}
                                aria-label={t("common.edit")}
                              >
                                {disabled ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Edit className="h-4 w-4" />}
                              </Button>
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                onClick={() => deleteUser(user)}
                                disabled={disabled}
                                title={t("common.delete")}
                                aria-label={t("common.delete")}
                                className="text-destructive hover:text-destructive"
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            <div className="flex flex-col gap-3 rounded-lg border bg-card px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between">
              <p className="text-xs text-muted-foreground">
                {t("common.showing")} {items.length} / {total}
              </p>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((prev) => Math.max(1, prev - 1))}
                >
                  {t("adminUsers.previous")}
                </Button>
                <span className="text-xs font-medium">
                  {page} / {pageCount}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={page >= pageCount}
                  onClick={() => setPage((prev) => Math.min(pageCount, prev + 1))}
                >
                  {t("adminUsers.next")}
                </Button>
              </div>
            </div>
          </div>
        )}
      </div>

      <UserFormModal
        open={modalOpen}
        onOpenChange={setModalOpen}
        roles={roles}
        user={editingUser}
        submitting={submitting}
        onSubmit={submitUser}
      />
    </AdminLayout>
  );
}
