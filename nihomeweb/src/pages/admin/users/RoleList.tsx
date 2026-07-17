import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { ChevronDown, Plus, ShieldCheck, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { Can } from "@/components/auth/Can";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
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
    // Capture the previous snapshot BEFORE setDraft, because setDraft's
    // updater runs asynchronously while the ref update below is immediate.
    // Without this, the updater would see the just-written value and think
    // the draft is "still in sync" with the new server data, clobbering
    // pending edits.
    const prevSynced = lastSyncedRef.current;
    setDraft((prev) => {
      const next: DirtyMap = { ...prev };
      for (const idStr of Object.keys(serverMap)) {
        const id = Number(idStr);
        const serverSet: Set<string> = serverMap[id]!;
        const currentDraft = next[id];
        const prevServer = prevSynced[id];
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
      const data = err.response?.data as { detail?: string; message?: string; error?: string } | undefined;
      message = data?.detail ?? data?.message ?? data?.error ?? err.message;
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("adminRbac.title")}</h1>
            <p className="text-xs italic text-muted-foreground">{t("adminRbac.description")}</p>
          </div>
          <Can permission={PERM_MANAGE}>
            <Button onClick={() => setCreateOpen(true)} data-testid="rbac-create-role">
              <Plus className="mr-1.5 h-4 w-4" />
              {t("adminRbac.createRole")}
            </Button>
          </Can>
        </header>

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
          <>
            {/* Mobile / tablet card view (<lg). The matrix layout is
                fundamentally 2D so we transpose it: one card per role, each
                with the full permission checklist. Groups by module prefix
                keep long lists scannable. Shares the same draft state as
                the desktop matrix so edits made in either view sync. */}
            <div className="space-y-3 lg:hidden">
              {roles.map((role) => {
                const set = draft[role.id] ?? serverMap[role.id];
                const dirty = isDirty(role.id);
                const grantedCount = set?.size ?? 0;
                return (
                  <article
                    key={role.id}
                    className="rounded-lg border bg-card shadow-sm"
                    data-testid={`rbac-card-${role.code}`}
                  >
                    <header className="flex flex-wrap items-start justify-between gap-2 border-b p-3">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <ShieldCheck className="h-4 w-4 shrink-0 text-muted-foreground" />
                          <h3 className="truncate text-sm font-semibold">
                            {role.labelKey ? t(role.labelKey) : role.name}
                          </h3>
                        </div>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {role.code} · {role.userCount} {t("adminRbac.usersAbbrev")}
                        </p>
                      </div>
                      {role.isSystem ? (
                        <Badge variant="outline" className="border-slate-200 bg-slate-100 text-slate-600">
                          {t("adminRbac.systemRoleBadge")}
                        </Badge>
                      ) : (
                        <Can permission={PERM_MANAGE}>
                          <div className="flex flex-wrap items-center gap-1">
                            <Button
                              size="sm"
                              variant="outline"
                              disabled={!dirty || savePermsMutation.isPending}
                              onClick={() =>
                                savePermsMutation.mutate({
                                  roleId: role.id,
                                  permissions: Array.from(draft[role.id] ?? []),
                                })
                              }
                            >
                              {t("adminRbac.save")}
                            </Button>
                            <Button size="sm" variant="ghost" disabled={!dirty} onClick={() => resetRole(role.id)}>
                              {t("adminRbac.reset")}
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-destructive hover:text-destructive"
                              onClick={() => setDeleteTarget(role)}
                              title={t("adminRbac.deleteRole")}
                              aria-label={t("adminRbac.deleteRole")}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        </Can>
                      )}
                    </header>

                    {/* Collapsed by default on mobile to keep the list scannable.
                        Native <details> gives us open/close for free. The summary
                        exposes the granted-count so users can compare roles without
                        expanding each card. */}
                    <details className="group">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-2 px-3 py-2 text-sm font-medium hover:bg-muted/40 [&::-webkit-details-marker]:hidden">
                        <span className="text-muted-foreground">
                          {t("adminRbac.permissionColumn")}
                          <span className="ml-2 text-xs">
                            ({grantedCount}/{perms.length})
                          </span>
                        </span>
                        <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-180" />
                      </summary>
                      <ul className="divide-y border-t">
                        {perms.map((perm) => {
                          const checked = set?.has(perm.code) ?? false;
                          const disabled = role.isSystem || !canManage;
                          const inputId = `rbac-m-${role.id}-${perm.id}`;
                          return (
                            <li key={perm.id} className="flex items-start gap-3 px-3 py-2">
                              <input
                                id={inputId}
                                type="checkbox"
                                className="mt-0.5 h-4 w-4 cursor-pointer disabled:cursor-not-allowed"
                                checked={checked}
                                disabled={disabled}
                                onChange={() => togglePerm(role.id, perm.code)}
                              />
                              <label htmlFor={inputId} className="min-w-0 flex-1 cursor-pointer">
                                <span className="block text-sm font-medium leading-tight">
                                  {t(`rbac.perm.${perm.code}.label`)}
                                </span>
                                <span className="block break-all text-xs text-muted-foreground">
                                  {perm.code}
                                </span>
                              </label>
                            </li>
                          );
                        })}
                      </ul>
                    </details>
                  </article>
                );
              })}
            </div>

            {/* Desktop matrix view (lg+) */}
            <div className="hidden overflow-x-auto rounded-lg border lg:block">
              <table className="w-full text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="sticky left-0 z-10 min-w-[260px] bg-muted/50 px-4 py-3 text-left font-medium">
                    {t("adminRbac.permissionColumn")}
                  </th>
                  {roles.map((role) => (
                    <th
                      key={role.id}
                      className="min-w-[160px] px-3 py-3 text-center font-medium"
                      data-testid={`rbac-col-${role.code}`}
                    >
                      <div className="flex items-center justify-center gap-1.5 normal-case">
                        <ShieldCheck className="h-4 w-4 text-muted-foreground" />
                        <span>{role.labelKey ? t(role.labelKey) : role.name}</span>
                      </div>
                      <div className="mt-0.5 text-[10px] font-normal normal-case text-muted-foreground">
                        {role.code} · {role.userCount} {t("adminRbac.usersAbbrev")}
                      </div>
                      {role.isSystem ? (
                        <Badge variant="outline" className="mt-1 border-slate-200 bg-slate-100 text-slate-600 normal-case">
                          {t("adminRbac.systemRoleBadge")}
                        </Badge>
                      ) : (
                        <div className="mt-1 flex items-center justify-center gap-1 normal-case">
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
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </Can>
                        </div>
                      )}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y">
                {perms.map((perm) => (
                  <tr key={perm.id} className="hover:bg-muted/40 transition">
                    <td className="sticky left-0 z-10 bg-background px-4 py-3 font-medium">
                      <div>{t(`rbac.perm.${perm.code}.label`)}</div>
                      <div className="text-xs font-normal text-muted-foreground">{perm.code}</div>
                    </td>
                    {roles.map((role) => {
                      const set = draft[role.id] ?? serverMap[role.id];
                      const checked = set?.has(perm.code) ?? false;
                      const disabled = role.isSystem || !canManage;
                      return (
                        <td key={role.id} className="px-3 py-3 text-center">
                          <input
                            type="checkbox"
                            className="h-4 w-4 cursor-pointer disabled:cursor-not-allowed"
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
          </>
        )}
      </div>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="w-[95vw] max-w-md max-h-[90vh] overflow-y-auto sm:w-full">
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
                className={
                  createCode.length > 0 && !codeValid
                    ? "text-xs text-destructive"
                    : "text-xs text-muted-foreground"
                }
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
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
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
        <AlertDialogContent className="w-[95vw] max-w-md sm:w-full">
          <AlertDialogHeader>
            <AlertDialogTitle>{t("adminRbac.deleteRole")}</AlertDialogTitle>
            <AlertDialogDescription>
              {deleteTarget
                ? t("adminRbac.confirmDelete").replace("{code}", deleteTarget.code)
                : ""}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter className="flex-col-reverse gap-2 sm:flex-row">
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
