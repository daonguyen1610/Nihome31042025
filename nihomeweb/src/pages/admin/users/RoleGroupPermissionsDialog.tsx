import { useState } from "react";
import { isAxiosError } from "axios";
import { useMutation } from "@tanstack/react-query";
import { ListChecks } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useToast } from "@/hooks/use-toast";
import { useI18n } from "@/lib/i18n";
import { rbacApi, type PermissionResponse, type RoleGroupResponse } from "@/services/rbacApi";

interface Props {
  group: RoleGroupResponse;
  permissions: PermissionResponse[];
  canManage: boolean;
  onSaved: () => Promise<void>;
}

export default function RoleGroupPermissionsDialog({
  group,
  permissions,
  canManage,
  onSaved,
}: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const [saved, setSaved] = useState<string[]>([]);
  const [draft, setDraft] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState("");
  const [moduleFilter, setModuleFilter] = useState("");
  const dirty = saved.length !== draft.size || saved.some((code) => !draft.has(code));
  const modules = Array.from(
    new Set(permissions.map((permission) => permission.code.split(".")[0])),
  ).sort();
  const term = search.trim().toLowerCase();
  const visible = permissions.filter(
    (permission) =>
      (!moduleFilter || permission.code.startsWith(`${moduleFilter}.`)) &&
      (!term ||
        permission.code.toLowerCase().includes(term) ||
        t(`rbac.perm.${permission.code}.label`).toLowerCase().includes(term)),
  );
  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!canManage) return;
      return (await rbacApi.updateRoleGroupBaseline(group.id, { permissions: Array.from(draft) }))
        .data;
    },
    onSuccess: async (response) => {
      if (!response) return;
      setSaved(response.baselinePermissions);
      setDraft(new Set(response.baselinePermissions));
      await onSaved();
      toast({ title: t("adminRbac.toast.baselineSaved") });
    },
    onError: () =>
      toast({ title: t("adminRbac.toast.baselineSaveFailed"), variant: "destructive" }),
  });
  const changeOpen = (nextOpen: boolean) => {
    if (saveMutation.isPending) return;
    if (!nextOpen && dirty) {
      setConfirmClose(true);
      return;
    }
    if (nextOpen) {
      setSaved(group.baselinePermissions);
      setDraft(new Set(group.baselinePermissions));
      setSearch("");
      setModuleFilter("");
      saveMutation.reset();
    }
    setOpen(nextOpen);
  };

  return (
    <>
      <Dialog open={open} onOpenChange={changeOpen}>
        <DialogTrigger asChild>
          <Button className="w-full sm:w-auto" data-testid="rbac-open-group-permissions">
            <ListChecks className="mr-2 h-4 w-4" />
            {t(canManage ? "adminRbac.groups.editPermissions" : "adminRbac.groups.viewPermissions")}
          </Button>
        </DialogTrigger>
        <DialogContent
          className="flex h-[calc(100dvh-2rem)] max-w-4xl flex-col gap-0 overflow-hidden p-0 sm:p-0"
          data-testid="rbac-group-permissions-dialog"
        >
          <DialogHeader className="shrink-0 border-b p-4 pr-10 text-left sm:p-6 sm:pr-12">
            <DialogTitle className="leading-snug">
              {t("adminRbac.groups.permissionsTitle")} · {t(group.labelKey)}
            </DialogTitle>
            <DialogDescription>{t("adminRbac.groups.baselineHelp")}</DialogDescription>
          </DialogHeader>
          <div className="grid shrink-0 gap-3 border-b p-4 sm:grid-cols-[2fr_1fr]">
            <div className="min-w-0 space-y-1">
              <Label htmlFor="rbac-baseline-search">{t("adminRbac.filter.search")}</Label>
              <Input
                id="rbac-baseline-search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t("adminRbac.filter.searchPlaceholder")}
              />
            </div>
            <div className="min-w-0 space-y-1">
              <Label htmlFor="rbac-baseline-module">{t("adminRbac.filter.module")}</Label>
              <select
                id="rbac-baseline-module"
                className="h-10 w-full rounded-md border border-input bg-background px-3 text-sm"
                value={moduleFilter}
                onChange={(event) => setModuleFilter(event.target.value)}
              >
                <option value="">{t("adminRbac.filter.allModules")}</option>
                {modules.map((module) => (
                  <option key={module} value={module}>
                    {t(`adminRbac.module.${module}`)}
                  </option>
                ))}
              </select>
            </div>
            <p className="text-xs text-muted-foreground sm:col-span-2" aria-live="polite">
              {t("adminRbac.groups.permissionSummary", {
                selected: draft.size,
                total: permissions.length,
                visible: visible.length,
              })}
              {dirty && (
                <span className="ml-2 font-semibold text-primary">{t("adminRbac.unsaved")}</span>
              )}
            </p>
          </div>
          <div
            className="min-h-0 flex-1 overflow-y-auto overscroll-contain p-4"
            tabIndex={0}
            role="region"
            aria-label={t("adminRbac.groups.permissionsTitle")}
          >
            {visible.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted-foreground">
                {t("adminRbac.noPermissions")}
              </p>
            ) : (
              modules.map((module) => {
                const items = visible.filter((permission) =>
                  permission.code.startsWith(`${module}.`),
                );
                if (items.length === 0) return null;
                return (
                  <section key={module} className="mb-5 last:mb-0">
                    <h3 className="mb-2 font-semibold">{t(`adminRbac.module.${module}`)}</h3>
                    <div className="grid gap-2 sm:grid-cols-2">
                      {items.map((permission) => (
                        <label
                          key={permission.id}
                          className="flex min-w-0 items-start gap-3 rounded-lg border p-3 hover:bg-muted/40"
                        >
                          <Checkbox
                            className="mt-0.5"
                            checked={draft.has(permission.code)}
                            disabled={!canManage || saveMutation.isPending}
                            aria-label={`${group.code} ${permission.code}`}
                            onCheckedChange={(checked) =>
                              setDraft((current) => {
                                const next = new Set(current);
                                if (checked === true) next.add(permission.code);
                                else next.delete(permission.code);
                                return next;
                              })
                            }
                          />
                          <span className="min-w-0">
                            <span className="block text-sm font-medium">
                              {t(`rbac.perm.${permission.code}.label`)}
                            </span>
                            <span className="mt-1 block break-all font-mono text-xs text-muted-foreground">
                              {permission.code}
                            </span>
                          </span>
                        </label>
                      ))}
                    </div>
                  </section>
                );
              })
            )}
          </div>
          <div className="shrink-0 border-t p-4">
            {saveMutation.isError && (
              <p role="alert" className="mb-3 text-sm text-destructive">
                {t(
                  isAxiosError(saveMutation.error) && saveMutation.error.response?.status === 403
                    ? "adminRbac.groups.permissionDenied"
                    : "adminRbac.groups.retrySave",
                )}
              </p>
            )}
            <DialogFooter className="gap-2 sm:space-x-0">
              <Button
                variant="outline"
                onClick={() => changeOpen(false)}
                disabled={saveMutation.isPending}
              >
                {t("common.close")}
              </Button>
              {canManage && (
                <>
                  <Button
                    variant="outline"
                    disabled={!dirty || saveMutation.isPending}
                    onClick={() => {
                      setDraft(new Set(saved));
                      saveMutation.reset();
                    }}
                    data-testid="rbac-reset-baseline"
                  >
                    {t("adminRbac.reset")}
                  </Button>
                  <Button
                    disabled={!dirty || saveMutation.isPending}
                    onClick={() => saveMutation.mutate()}
                    data-testid="rbac-save-baseline"
                  >
                    {t("adminRbac.groups.saveBaseline")}
                  </Button>
                </>
              )}
            </DialogFooter>
          </div>
        </DialogContent>
      </Dialog>
      <AlertDialog open={confirmClose} onOpenChange={setConfirmClose}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("adminRbac.groups.discardTitle")}</AlertDialogTitle>
            <AlertDialogDescription>{t("adminRbac.groups.discardHelp")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("adminRbac.groups.keepEditing")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                setConfirmClose(false);
                setOpen(false);
              }}
            >
              {t("adminRbac.groups.discard")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
