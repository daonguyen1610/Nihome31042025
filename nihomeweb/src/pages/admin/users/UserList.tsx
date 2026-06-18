import { useCallback, useEffect, useMemo, useState } from "react";
import { Edit, Plus, RefreshCw, Search, Trash2, UserRound } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Switch } from "@/components/ui/switch";
import { useToast } from "@/hooks/use-toast";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type CreateUserRequest,
  type RoleCatalogResponse,
  type UpdateUserRequest,
  type UserDetailResponse,
  type UserListItemResponse,
} from "@/services/adminApi";
import { newIdempotencyKey } from "@/lib/api";
import UserFormModal from "./UserFormModal";

const PAGE_SIZE = 20;

const roleStyles: Record<string, { background: string; color: string }> = {
  SUPER_ADMIN: { background: "hsl(var(--admin-danger-soft))", color: "hsl(var(--admin-danger))" },
  ADMIN: { background: "hsl(var(--admin-primary-soft))", color: "hsl(var(--admin-primary))" },
  USER: { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" },
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
    error.response.data !== null &&
    "message" in error.response.data &&
    typeof error.response.data.message === "string"
  ) {
    return error.response.data.message;
  }

  return undefined;
};

export default function UserList() {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<UserListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [catalog, setCatalog] = useState<RoleCatalogResponse | null>(null);
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

  const roles = useMemo(() => catalog?.roles ?? [], [catalog]);
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const roleLabelMap = useMemo(
    () => new Map(roles.map((item) => [item.role, t(item.labelKey)])),
    [roles, t],
  );

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
        adminApi.getUserRoles(),
      ]);
      setItems(usersRes.data.items);
      setTotal(usersRes.data.total);
      setCatalog(rolesRes.data);
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

  const applySearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPage(1);
    setSearch(searchText.trim());
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-7">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">{t("adminUsers.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {total} {t("adminUsers.totalUsers")}
          </p>
        </div>
        <button onClick={openCreate} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Plus className="w-4 h-4" /> {t("adminUsers.addUser")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <form onSubmit={applySearch} className="grid grid-cols-1 lg:grid-cols-[1fr_220px_auto] gap-3">
          <div className="relative">
            <Search className="w-4 h-4 absolute left-4 top-1/2 -translate-y-1/2" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={searchText}
              onChange={(event) => setSearchText(event.target.value)}
              className="admin-input w-full pl-10"
              placeholder={t("adminUsers.searchPlaceholder")}
            />
          </div>
          <select
            value={role}
            onChange={(event) => {
              setPage(1);
              setRole(event.target.value);
            }}
            className="admin-input w-full"
          >
            <option value="">{t("adminUsers.allRoles")}</option>
            {roles.map((item) => (
              <option key={item.role} value={item.role}>
                {t(item.labelKey)}
              </option>
            ))}
          </select>
          <button type="submit" className="admin-btn-primary inline-flex items-center justify-center gap-2 px-5 py-2.5 text-sm">
            <Search className="w-4 h-4" /> {t("common.search")}
          </button>
        </form>
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={loadData} />
      ) : (
        <div className="admin-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left border-b" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <th className="px-5 py-3 font-semibold">{t("adminUsers.user")}</th>
                  <th className="px-5 py-3 font-semibold">{t("adminUsers.phoneNumber")}</th>
                  <th className="px-5 py-3 font-semibold">{t("adminUsers.email")}</th>
                  <th className="px-5 py-3 font-semibold">{t("adminUsers.role")}</th>
                  <th className="px-5 py-3 font-semibold">{t("common.status")}</th>
                  <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y" style={{ borderColor: "hsl(var(--admin-border))" }}>
                {items.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-5 py-12 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                      {t("adminUsers.empty")}
                    </td>
                  </tr>
                ) : (
                  items.map((user) => {
                    const roleStyle = roleStyles[user.role] ?? roleStyles.USER;
                    const disabled = busyUserId === user.id;

                    return (
                      <tr key={user.id} className="align-middle">
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-3">
                            <div
                              className="w-10 h-10 rounded-full text-white flex items-center justify-center font-bold text-sm shrink-0"
                              style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary)), hsl(22 95% 58%))" }}
                            >
                              {user.avatarUrl ? (
                                <img src={user.avatarUrl} alt="" className="w-full h-full rounded-full object-cover" />
                              ) : (
                                user.fullName?.[0]?.toUpperCase() ?? <UserRound className="w-4 h-4" />
                              )}
                            </div>
                            <div className="min-w-0">
                              <p className="font-bold truncate">{user.fullName ?? t("adminUsers.unnamed")}</p>
                              <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                                ID #{user.id}
                              </p>
                            </div>
                          </div>
                        </td>
                        <td className="px-5 py-4 font-semibold whitespace-nowrap">{user.phoneNumber}</td>
                        <td className="px-5 py-4">{user.email ?? "—"}</td>
                        <td className="px-5 py-4">
                          <span className="admin-chip" style={roleStyle}>
                            {roleLabelMap.get(user.role) ?? user.role}
                          </span>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2">
                            <Switch
                              checked={user.isActive}
                              onCheckedChange={() => toggleActive(user)}
                              disabled={disabled}
                              aria-label={t("adminUsers.active")}
                            />
                            <span className="text-xs font-semibold" style={{ color: "hsl(var(--admin-muted))" }}>
                              {user.isActive ? t("adminUsers.active") : t("adminUsers.inactive")}
                            </span>
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              type="button"
                              onClick={() => openEdit(user)}
                              disabled={disabled}
                              className="p-2 rounded-lg hover:bg-muted transition disabled:opacity-50"
                              title={t("common.edit")}
                            >
                              {disabled ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Edit className="w-4 h-4" />}
                            </button>
                            <button
                              type="button"
                              onClick={() => deleteUser(user)}
                              disabled={disabled}
                              className="p-2 rounded-lg hover:bg-red-50 text-red-500 transition disabled:opacity-50"
                              title={t("common.delete")}
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 px-5 py-4 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
            <p className="text-xs font-semibold" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("common.showing")} {items.length} / {total}
            </p>
            <div className="flex items-center gap-2">
              <button
                type="button"
                className="admin-btn-primary opacity-70 px-4 py-2 text-xs"
                disabled={page <= 1}
                onClick={() => setPage((prev) => Math.max(1, prev - 1))}
              >
                {t("adminUsers.previous")}
              </button>
              <span className="text-xs font-bold">
                {page} / {pageCount}
              </span>
              <button
                type="button"
                className="admin-btn-primary opacity-70 px-4 py-2 text-xs"
                disabled={page >= pageCount}
                onClick={() => setPage((prev) => Math.min(pageCount, prev + 1))}
              >
                {t("adminUsers.next")}
              </button>
            </div>
          </div>
        </div>
      )}

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
