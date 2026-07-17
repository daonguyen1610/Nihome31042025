import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Pencil, Trash2, Check, X, Search as SearchIcon, ArrowUp, ArrowDown } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type WorkflowConfig,
  type WorkflowStep,
  type UpsertWorkflowConfigRequest,
} from "@/services/adminApi";
import { rbacApi, type RoleResponse } from "@/services/rbacApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { PageLoading, PageError } from "@/components/PageState";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

// Same regex as the backend normalization: lowercase slug, digits, dash / dot.
const IDENTIFIER_RE = /^[a-z0-9][a-z0-9._-]{0,58}[a-z0-9]$|^[a-z0-9]$/;

type FormData = {
  module: string;
  action: string;
  name: string;
  description: string;
  isActive: boolean;
  sortOrder: number;
  steps: WorkflowStep[];
};

const blankStep = (order: number): WorkflowStep => ({
  order,
  name: "",
  approverRoleCode: "",
  slaHours: 24,
  requireAllApprovers: false,
  conditionExpression: null,
});

const emptyForm: FormData = {
  module: "",
  action: "",
  name: "",
  description: "",
  isActive: true,
  sortOrder: 0,
  steps: [blankStep(1)],
};

const getErrorMessage = (error: unknown): string | undefined => {
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

const Workflows = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.workflowManage);

  const [workflows, setWorkflows] = useState<WorkflowConfig[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [roles, setRoles] = useState<RoleResponse[]>([]);
  const [query, setQuery] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [w, r] = await Promise.all([
        adminApi.listWorkflows(true),
        rbacApi.listRoles(),
      ]);
      setWorkflows(w.data);
      setRoles(r.data.filter((role) => role.isActive));
    } catch (err) {
      setError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return workflows;
    return workflows.filter(
      (w) =>
        w.module.toLowerCase().includes(q) ||
        w.action.toLowerCase().includes(q) ||
        w.name.toLowerCase().includes(q),
    );
  }, [workflows, query]);

  const visibleIds = useMemo(() => filtered.map((w) => w.id), [filtered]);
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
    deleteOne: (id) => adminApi.deleteWorkflow(id),
    onAfter: async () => {
      await load();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [query, clearSelection]);

  // -------- dialog / form --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormData>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: workflows.length });
    setFormError(null);
    setDialogOpen(true);
  };
  const openEdit = (wf: WorkflowConfig) => {
    setEditingId(wf.id);
    setForm({
      module: wf.module,
      action: wf.action,
      name: wf.name,
      description: wf.description ?? "",
      isActive: wf.isActive,
      sortOrder: wf.sortOrder,
      steps: wf.steps.length > 0 ? wf.steps.map((s) => ({ ...s })) : [blankStep(1)],
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const patchStep = (index: number, patch: Partial<WorkflowStep>) => {
    setForm((prev) => ({
      ...prev,
      steps: prev.steps.map((s, i) => (i === index ? { ...s, ...patch } : s)),
    }));
  };
  const addStep = () => {
    setForm((prev) => ({
      ...prev,
      steps: [...prev.steps, blankStep(prev.steps.length + 1)],
    }));
  };
  const removeStep = (index: number) => {
    setForm((prev) => {
      if (prev.steps.length <= 1) return prev;
      return {
        ...prev,
        steps: prev.steps
          .filter((_, i) => i !== index)
          .map((s, i) => ({ ...s, order: i + 1 })),
      };
    });
  };
  const moveStep = (index: number, direction: -1 | 1) => {
    setForm((prev) => {
      const next = [...prev.steps];
      const target = index + direction;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return { ...prev, steps: next.map((s, i) => ({ ...s, order: i + 1 })) };
    });
  };

  const submit = async () => {
    setFormError(null);
    const module = form.module.trim().toLowerCase();
    const action = form.action.trim().toLowerCase();
    const name = form.name.trim();
    if (!module || !action || !name) {
      setFormError(t("form.required"));
      return;
    }
    if (!IDENTIFIER_RE.test(module) || !IDENTIFIER_RE.test(action)) {
      setFormError(t("workflow.identifierInvalid"));
      return;
    }
    if (form.steps.length === 0) {
      setFormError(t("workflow.stepsRequired"));
      return;
    }
    for (const s of form.steps) {
      if (!s.name.trim() || !s.approverRoleCode) {
        setFormError(t("workflow.stepFieldsRequired"));
        return;
      }
    }
    const payload: UpsertWorkflowConfigRequest = {
      module,
      action,
      name,
      description: form.description.trim() || null,
      isActive: form.isActive,
      sortOrder: Number.isFinite(form.sortOrder) ? Math.max(0, form.sortOrder) : 0,
      steps: form.steps.map((s, i) => ({
        order: i + 1,
        name: s.name.trim(),
        approverRoleCode: s.approverRoleCode,
        slaHours: Number.isFinite(s.slaHours) ? Math.max(0, s.slaHours) : 0,
        requireAllApprovers: s.requireAllApprovers,
        conditionExpression: s.conditionExpression?.trim() || null,
      })),
    };
    setSaving(true);
    try {
      if (editingId != null) {
        await adminApi.updateWorkflow(editingId, payload);
        toast({ title: t("form.updated") });
      } else {
        await adminApi.createWorkflow(payload);
        toast({ title: t("form.created") });
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      setFormError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (wf: WorkflowConfig) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteWorkflow(wf.id);
      toast({ title: t("form.deleted") });
      await load();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err),
        variant: "destructive",
      });
    }
  };

  if (loading) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6">
          <PageLoading />
        </div>
      </AdminLayout>
    );
  }
  if (error) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6">
          <PageError message={error} onRetry={() => void load()} />
        </div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <h1 className="text-2xl font-semibold">{t("workflow.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("workflow.subtitle")}</p>
          </div>
          {canManage && (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("workflow.newWorkflow")}
            </Button>
          )}
        </header>

        {/* Search */}
        <section className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
          <div className="min-w-[220px] flex-1 sm:max-w-sm">
            <Label className="text-xs" htmlFor="wf-search">
              {t("common.search")}
            </Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="wf-search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t("workflow.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          <p className="text-xs italic text-muted-foreground">
            {filtered.length} / {workflows.length}
          </p>
        </section>

        {filtered.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <SearchIcon className="h-5 w-5" aria-hidden />
            </div>
            {workflows.length === 0 ? (
              <>
                <p>{t("workflow.empty")}</p>
                {canManage && (
                  <Button size="sm" onClick={openCreate}>
                    <Plus className="mr-1.5 h-4 w-4" /> {t("workflow.newWorkflow")}
                  </Button>
                )}
              </>
            ) : (
              <>
                <p>{t("workflow.noMatch")}</p>
                <Button variant="outline" size="sm" onClick={() => setQuery("")}>
                  {t("common.reset")}
                </Button>
              </>
            )}
          </div>
        ) : (
          <div className="space-y-2">
            {canManage && (
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={clearSelection}
                onBulkDelete={() => void handleBulkDelete()}
              />
            )}

            {/* Mobile card view (<lg) */}
            <ul className="grid gap-3 lg:hidden">
              {filtered.map((w) => (
                <li key={w.id} className="rounded-lg border bg-card p-3 shadow-sm">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex min-w-0 items-start gap-2">
                      {canManage && (
                        <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                          <Checkbox
                            checked={selectedIds.has(w.id)}
                            onCheckedChange={(v) => toggleOne(w.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${w.name}`}
                          />
                        </span>
                      )}
                      <div className="min-w-0">
                        <h3 className="break-words text-sm font-semibold leading-tight">{w.name}</h3>
                        <p className="mt-0.5 break-all font-mono text-xs text-muted-foreground">
                          {w.module}.{w.action}
                        </p>
                      </div>
                    </div>
                    {w.isActive ? (
                      <Check className="h-4 w-4 shrink-0 text-emerald-600" aria-label={t("workflow.field.isActive")} />
                    ) : (
                      <X className="h-4 w-4 shrink-0 text-muted-foreground" aria-label={t("workflow.field.isActive")} />
                    )}
                  </div>
                  <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                    <dt className="text-muted-foreground">{t("workflow.field.steps")}</dt>
                    <dd className="font-medium">{w.steps.length}</dd>
                    {w.description && (
                      <>
                        <dt className="text-muted-foreground">{t("workflow.field.description")}</dt>
                        <dd className="break-words">{w.description}</dd>
                      </>
                    )}
                  </dl>
                  {canManage && (
                    <div className="mt-3 flex flex-wrap items-center justify-end gap-1 border-t pt-2">
                      <Button variant="ghost" size="sm" onClick={() => openEdit(w)}>
                        <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => remove(w)}
                        className="text-destructive hover:text-destructive"
                      >
                        <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>

            {/* Desktop table (lg+) */}
            <div className="hidden overflow-x-auto rounded-lg border lg:block">
              <table className="w-full min-w-[900px] divide-y text-sm">
                <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    {canManage && (
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
                    )}
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("workflow.field.module")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("workflow.field.action")}</th>
                    <th className="min-w-[200px] px-3 py-3 text-left font-medium">{t("workflow.field.name")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("workflow.field.steps")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("workflow.field.isActive")}</th>
                    {canManage && (
                      <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {filtered.map((w) => (
                    <tr key={w.id} className="hover:bg-muted/40 transition">
                      {canManage && (
                        <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selectedIds.has(w.id)}
                            onCheckedChange={(v) => toggleOne(w.id, v === true)}
                            aria-label={`${t("common.selectAll")} · ${w.name}`}
                          />
                        </td>
                      )}
                      <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{w.module}</td>
                      <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{w.action}</td>
                      <td className="min-w-[200px] px-3 py-3 font-medium">
                        {w.name}
                        {w.description && (
                          <p className="mt-0.5 text-xs text-muted-foreground">{w.description}</p>
                        )}
                      </td>
                      <td className="whitespace-nowrap px-3 py-3 text-center">{w.steps.length}</td>
                      <td className="whitespace-nowrap px-3 py-3">
                        {w.isActive ? (
                          <Badge variant="outline" className="border-emerald-200 bg-emerald-50 text-emerald-700">
                            <Check className="mr-1 h-3 w-3" />
                            {t("common.on")}
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-600">
                            <X className="mr-1 h-3 w-3" />
                            {t("common.off")}
                          </Badge>
                        )}
                      </td>
                      {canManage && (
                        <td className="whitespace-nowrap px-3 py-3 text-right">
                          <div className="inline-flex items-center gap-1">
                            <Button variant="ghost" size="sm" onClick={() => openEdit(w)}>
                              <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => remove(w)}
                              className="text-destructive hover:text-destructive"
                            >
                              <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                            </Button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      <Dialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditingId(null);
            setFormError(null);
          }
        }}
      >
        <DialogContent className="w-[95vw] max-w-2xl max-h-[90vh] overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle>
              {editingId != null
                ? t("workflow.editModalTitle")
                : t("workflow.createModalTitle")}
            </DialogTitle>
            <DialogDescription>{t("workflow.dialogHelp")}</DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="wf-module" className="text-xs">
                  {t("workflow.field.module")} *
                </Label>
                <Input
                  id="wf-module"
                  value={form.module}
                  onChange={(e) => setForm({ ...form, module: e.target.value.toLowerCase() })}
                  placeholder="quotes"
                  className="h-9 font-mono"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="wf-action" className="text-xs">
                  {t("workflow.field.action")} *
                </Label>
                <Input
                  id="wf-action"
                  value={form.action}
                  onChange={(e) => setForm({ ...form, action: e.target.value.toLowerCase() })}
                  placeholder="approve"
                  className="h-9 font-mono"
                />
              </div>
            </div>
            <p className="text-xs text-muted-foreground">{t("workflow.identifierHint")}</p>

            <div className="space-y-1.5">
              <Label htmlFor="wf-name" className="text-xs">
                {t("workflow.field.name")} *
              </Label>
              <Input
                id="wf-name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className="h-9"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="wf-description" className="text-xs">
                {t("workflow.field.description")}
              </Label>
              <Textarea
                id="wf-description"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={2}
              />
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="wf-sortOrder" className="text-xs">
                  {t("workflow.field.sortOrder")}
                </Label>
                <Input
                  id="wf-sortOrder"
                  type="number"
                  min={0}
                  value={form.sortOrder}
                  onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) || 0 })}
                  className="h-9"
                />
              </div>
              <div className="flex items-end gap-2">
                <Switch
                  id="wf-active"
                  checked={form.isActive}
                  onCheckedChange={(checked) => setForm({ ...form, isActive: checked })}
                  aria-label={t("workflow.field.isActive")}
                />
                <Label htmlFor="wf-active" className="mb-2 text-sm font-medium">
                  {t("workflow.field.isActive")}
                </Label>
              </div>
            </div>

            {/* Steps editor */}
            <div className="space-y-2 rounded-md border p-3">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-semibold">{t("workflow.stepsTitle")}</h4>
                <Button type="button" size="sm" variant="outline" onClick={addStep}>
                  <Plus className="mr-1 h-3.5 w-3.5" /> {t("workflow.addStep")}
                </Button>
              </div>
              {form.steps.map((step, idx) => (
                <div
                  key={idx}
                  className="space-y-2 rounded border bg-card/50 p-2"
                  data-testid={`wf-step-${idx}`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-semibold uppercase text-muted-foreground">
                      #{idx + 1}
                    </span>
                    <div className="inline-flex items-center gap-1">
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        onClick={() => moveStep(idx, -1)}
                        disabled={idx === 0}
                        aria-label={t("workflow.moveUp")}
                        className="h-7 w-7"
                      >
                        <ArrowUp className="h-3.5 w-3.5" />
                      </Button>
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        onClick={() => moveStep(idx, 1)}
                        disabled={idx === form.steps.length - 1}
                        aria-label={t("workflow.moveDown")}
                        className="h-7 w-7"
                      >
                        <ArrowDown className="h-3.5 w-3.5" />
                      </Button>
                      <Button
                        type="button"
                        size="icon"
                        variant="ghost"
                        onClick={() => removeStep(idx)}
                        disabled={form.steps.length <= 1}
                        aria-label={t("common.delete")}
                        className="h-7 w-7 text-destructive hover:text-destructive"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </Button>
                    </div>
                  </div>
                  <div className="grid gap-2 sm:grid-cols-2">
                    <div className="space-y-1">
                      <Label className="text-xs">{t("workflow.field.stepName")} *</Label>
                      <Input
                        value={step.name}
                        onChange={(e) => patchStep(idx, { name: e.target.value })}
                        className="h-8"
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">{t("workflow.field.approverRole")} *</Label>
                      <Select
                        value={step.approverRoleCode}
                        onValueChange={(v) => patchStep(idx, { approverRoleCode: v })}
                      >
                        <SelectTrigger className="h-8">
                          <SelectValue placeholder={t("workflow.approverPlaceholder")} />
                        </SelectTrigger>
                        <SelectContent>
                          {roles.map((r) => (
                            <SelectItem key={r.code} value={r.code}>
                              {r.labelKey ? t(r.labelKey) : r.name} ({r.code})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">{t("workflow.field.slaHours")}</Label>
                      <Input
                        type="number"
                        min={0}
                        value={step.slaHours}
                        onChange={(e) => patchStep(idx, { slaHours: Number(e.target.value) || 0 })}
                        className="h-8"
                      />
                    </div>
                    <div className="flex items-end gap-2">
                      <Switch
                        checked={step.requireAllApprovers}
                        onCheckedChange={(v) => patchStep(idx, { requireAllApprovers: v })}
                        aria-label={t("workflow.field.requireAll")}
                      />
                      <Label className="mb-2 text-xs">{t("workflow.field.requireAll")}</Label>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {formError && <p className="text-sm text-destructive">{formError}</p>}
          </div>

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={saving}>
              {saving ? t("common.saving") : t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default Workflows;
