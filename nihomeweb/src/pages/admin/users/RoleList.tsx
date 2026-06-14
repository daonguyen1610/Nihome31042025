import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { Plus, ShieldCheck, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { Can } from "@/components/auth/Can";
import { PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useI18n } from "@/lib/i18n";
import {
  rbacApi,
  type PermissionResponse,
  type RolePermissionsResponse,
  type RoleResponse,
} from "@/services/rbacApi";

const PERM_MANAGE = "rbac.roles.manage";
// Mirror of backend CreateRoleRequest regex.
const ROLE_CODE_RE = /^[A-Z][A-Z0-9_]{1,49}$/;

type DirtyMap = Record<number, Set<string>>;

function setsEqual(a: Set<string>, b: Set<string>) {
  if (a.size !== b.size) return false;
  for (const v of a) if (!b.has(v)) return false;
  return true;
}

export default function RoleList() {
  const { t } = useI18n();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const { has } = usePermissions();
  const canManage = has(PERM_MANAGE);

  const rolesQuery = useQuery({
    queryKey: ["rbac", "roles"],
    queryFn: async () => (await rbacApi.listRoles()).data,
  });
  const permsQuery = useQuery({
    queryKey: ["rbac", "permissions"],
    queryFn: async () => (await rbacApi.listPermissions()).data,
  });

  const roleIds = rolesQuery.data?.map((r) => r.id) ?? [];
  const rolePermQueries = useQueries({
    queries: roleIds.map((id) => ({
      queryKey: ["rbac", "rolePermissions", id],
      queryFn: async () => (await rbacApi.getRolePermissions(id)).data,
      enabled: roleIds.length > 0,
    })),
  });

  const serverMap = useMemo<DirtyMap>(() => {
    const out: DirtyMap = {};
    rolePermQueries.forEach((q) => {
      const data = q.data as RolePermissionsResponse | undefined;
      if (data) out[data.role.id] = new Set(data.permissions);
    });
    return out;
  }, [rolePermQueries]);

  const [draft, setDraft] = useState<DirtyMap>({});
  // Tracks the server snapshot we last reconciled against, so we can tell
  // whether a draft is "untouched" (still equal to that snapshot) and is
  // safe to overwrite when a fresh server payload arrives without clobbering
  // pending edits.
  const lastSyncedRef = useRef<DirtyMap>({});
  useEffect(() => {
    setDraft((prev) => {
      const next: DirtyMap = { ...prev };
      for (const idStr of Object.keys(serverMap)) {
        const id = Number(idStr);
        const serverSet: Set<string> = serverMap[id]!;
        const currentDraft = next[id];
        const prevServer = lastSyncedRef.current[id];
        if (!currentDraft) {
          next[id] = new Set<string>(serverSet);
        } else if (prevServer && setsEqual(currentDraft, prevServer)) {
          next[id] = new Set<string>(serverSet);
        }
      }
      for (const idStr of Object.keys(next)) {
        if (!(idStr in serverMap)) delete next[Number(idStr)];
      }
      return next;
    });
    const snapshot: DirtyMap = {};
    for (const idStr of Object.keys(serverMap)) {
      const id = Number(idStr);
      snapshot[id] = new Set<string>(serverMap[id]!);
    }
    lastSyncedRef.current = snapshot;
  }, [serverMap]);

  const isDirty = (roleId: number) => {
    const a = draft[roleId];
    const b = serverMap[roleId];
    if (!a || !b) return false;
    return !setsEqual(a, b);
  };

  const togglePerm = (roleId: number, code: string) => {
    setDraft((prev) => {
      const next: DirtyMap = { ...prev };
      const set = new Set<string>(next[roleId] ?? []);
      if (set.has(code)) set.delete(code);
      else set.add(code);
      next[roleId] = set;
      return next;
    });
  };

  const resetRole = (roleId: number) => {
    setDraft((prev) => ({ ...prev, [roleId]: new Set(serverMap[roleId] ?? []) }));
  };

  const invalidateAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["rbac", "roles"] }),
      queryClient.invalidateQueries({ queryKey: ["rbac", "rolePermissions"] }),
      queryClient.invalidateQueries({ queryKey: ["me", "permissions"] }),
    ]);
  };

  const reportError = (err: unknown, defaultKey: string) => {
    let message: string = t(defaultKey);
    if (isAxiosError(err)) {
      const data = err.response?.data as { message?: string; error?: string } | undefined;
      message = data?.message ?? data?.error ?? err.message;
    } else if (err instanceof Error) {
      message = err.message;
    }
    toast({ title: t(defaultKey), description: message, variant: "destructive" });
  };

  const savePermsMutation = useMutation({
    mutationFn: async ({ roleId, permissions }: { roleId: number; permissions: string[] }) => {
      await rbacApi.updateRolePermissions(roleId, { permissions });
    },
    onSuccess: async () => {
      await invalidateAll();
      toast({ title: t("adminRbac.toast.saved") });
    },
    onError: (err) => reportError(err, "adminRbac.toast.saveFailed"),
  });

  const deleteMutation = useMutation({
    mutationFn: async (roleId: number) => {
      await rbacApi.deleteRole(roleId);
    },
    onSuccess: async () => {
      await invalidateAll();
      toast({ title: t("adminRbac.toast.deleted") });
    },
    onError: (err) => reportError(err, "adminRbac.toast.deleteFailed"),
  });

  const [createOpen, setCreateOpen] = useState(false);
  const [createCode, setCreateCode] = useState("");
  const [createName, setCreateName] = useState("");
  const codeValid = ROLE_CODE_RE.test(createCode.trim());
  const nameValid = createName.trim().length >= 2;
  const createMutation = useMutation({
    mutationFn: async () =>
      (await rbacApi.createRole({ code: createCode.trim(), name: createName.trim() })).data,
    onSuccess: async () => {
      setCreateOpen(false);
      setCreateCode("");
      setCreateName("");
      await invalidateAll();
      toast({ title: t("adminRbac.toast.created") });
    },
    onError: (err) => reportError(err, "adminRbac.toast.createFailed"),
  });

  const [deleteTarget, setDeleteTarget] = useState<RoleResponse | null>(null);

  const loading = rolesQuery.isLoading || permsQuery.isLoading;
  const error = rolesQuery.error ?? permsQuery.error;

  const roles: RoleResponse[] = rolesQuery.data ?? [];
  const perms: PermissionResponse[] = useMemo(
    () => (permsQuery.data ?? []).slice().sort((a, b) => a.code.localeCompare(b.code)),
    [permsQuery.data],
  );

  return (
    <AdminLayout>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
            {t("adminRbac.title")}
          </h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("adminRbac.description")}
          </p>
        </div>
        <Can permission={PERM_MANAGE}>
          <Button onClick={() => setCreateOpen(true)} data-testid="rbac-create-role">
            <Plus className="w-4 h-4 mr-2" />
            {t("adminRbac.createRole")}
          </Button>
        </Can>
      </div>

      {loading ? (
        <PageLoading />
      ) : error ? (
        <PageError
          message={error instanceof Error ? error.message : t("common.error")}
          onRetry={() => {
            void rolesQuery.refetch();
            void permsQuery.refetch();
          }}
        />
      ) : (
        <div className="admin-card overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr
                  className="text-left border-b"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                >
                  <th className="px-4 py-3 font-semibold sticky left-0 bg-background z-10 min-w-[260px]">
                    {t("adminRbac.permissionColumn")}
                  </th>
                  {roles.map((role) => (
                    <th
                      key={role.id}
                      className="px-3 py-3 font-semibold text-center min-w-[160px]"
                      data-testid={`rbac-col-${role.code}`}
                    >
                      <div className="flex items-center justify-center gap-1.5">
                        <ShieldCheck className="w-4 h-4 text-muted-foreground" />
                        <span>{role.labelKey ? t(role.labelKey) : role.name}</span>
                      </div>
                      <div
                        className="text-[10px] font-normal mt-0.5"
                        style={{ color: "hsl(var(--admin-muted))" }}
                      >
                        {role.code} · {role.userCount} {t("adminRbac.usersAbbrev")}
                      </div>
                      {role.isSystem ? (
                        <span className="admin-chip mt-1 inline-block">
                          {t("adminRbac.systemRoleBadge")}
                        </span>
                      ) : (
                        <div className="flex items-center justify-center gap-1 mt-1">
                          <Can permission={PERM_MANAGE}>
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={!isDirty(role.id) || savePermsMutation.isPending}
                              onClick={() =>
                                savePermsMutation.mutate({
                                  roleId: role.id,
                                  permissions: Array.from(draft[role.id] ?? []),
                                })
                              }
                              data-testid={`rbac-save-${role.code}`}
                            >
                              {t("adminRbac.save")}
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              disabled={!isDirty(role.id)}
                              onClick={() => resetRole(role.id)}
                            >
                              {t("adminRbac.reset")}
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-destructive hover:text-destructive"
                              onClick={() => setDeleteTarget(role)}
                              data-testid={`rbac-delete-${role.code}`}
                              title={t("adminRbac.deleteRole")}
                            >
                              <Trash2 className="w-4 h-4" />
                            </Button>
                          </Can>
                        </div>
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {perms.map((perm) => (
                  <tr
                    key={perm.id}
                    className="border-t"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  >
                    <td className="px-4 py-3 font-medium sticky left-0 bg-background z-10">
                      <div>{t(`rbac.perm.${perm.code}.label`)}</div>
                      <div
                        className="text-xs font-normal"
                        style={{ color: "hsl(var(--admin-muted))" }}
                      >
                        {perm.code}
                      </div>
                    </td>
                    {roles.map((role) => {
                      const set = draft[role.id] ?? serverMap[role.id];
                      const checked = set?.has(perm.code) ?? false;
                      const disabled = role.isSystem || !canManage;
                      return (
                        <td key={role.id} className="px-3 py-3 text-center">
                          <input
                            type="checkbox"
                            className="w-4 h-4 cursor-pointer disabled:cursor-not-allowed"
                            checked={checked}
                            disabled={disabled}
                            onChange={() => togglePerm(role.id, perm.code)}
                            aria-label={`${role.code} ${perm.code}`}
                          />
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("adminRbac.createRole")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="rbac-code">{t("adminRbac.codeLabel")}</Label>
              <Input
                id="rbac-code"
                value={createCode}
                onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
                placeholder="MARKETING"
                aria-invalid={createCode.length > 0 && !codeValid}
                data-testid="rbac-create-code"
              />
              <p
                className="text-xs"
                style={{
                  color:
                    createCode.length > 0 && !codeValid
                      ? "hsl(var(--destructive))"
                      : "hsl(var(--admin-muted))",
                }}
              >
                {t("adminRbac.codeHint")}
              </p>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="rbac-name">{t("adminRbac.nameLabel")}</Label>
              <Input
                id="rbac-name"
                value={createName}
                onChange={(e) => setCreateName(e.target.value)}
                placeholder="Marketing"
                data-testid="rbac-create-name"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setCreateOpen(false)}>
              {t("adminRbac.cancel")}
            </Button>
            <Button
              onClick={() => createMutation.mutate()}
              disabled={createMutation.isPending || !codeValid || !nameValid}
              data-testid="rbac-create-submit"
            >
              {createMutation.isPending ? t("adminRbac.creating") : t("adminRbac.create")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("adminRbac.deleteRole")}</AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget
                ? t("adminRbac.confirmDelete").replace("{code}", deleteTarget.code)
                : ""}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("adminRbac.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => {
                if (deleteTarget) {
                  const id = deleteTarget.id;
                  setDeleteTarget(null);
                  deleteMutation.mutate(id);
                }
              }}
              data-testid="rbac-delete-confirm"
            >
              {t("adminRbac.deleteRole")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AdminLayout>
  );
}
