import { useConfirm } from "@/components/ui/confirmation";
import { FieldScope, FieldControl } from "@/components/ui/field-scope";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Plus,
  Pencil,
  Trash2,
  Check,
  X,
  Search as SearchIcon,
  ArrowUp,
  ArrowDown,
  ChevronRight,
  Info,
  AlertTriangle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import {
  adminApi,
  type WorkflowConfig,
  type WorkflowStep,
  type WorkflowStepIssue,
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

/** Business actions people pick by name; their technical codes stay out of sight. */
const BUSINESS_ACTIONS = [
  { module: "quotes", action: "approve", labelKey: "workflow.business.quotesApprove" },
  { module: "contracts", action: "sign", labelKey: "workflow.business.contractsSign" },
  { module: "tenders", action: "submit", labelKey: "workflow.business.tendersSubmit" },
  {
    module: "tenders",
    action: "advance-stage",
    labelKey: "workflow.business.tendersAdvanceStage",
  },
  {
    module: "design.concepts",
    action: "approve",
    labelKey: "workflow.business.designConceptsApprove",
  },
  {
    module: "design.shop",
    action: "issue-for-construction",
    labelKey: "workflow.business.designShopIfc",
  },
] as const;
const OTHER_BUSINESS = "other";
const businessKeyOf = (module: string, action: string) => `${module}|${action}`;

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

const Workflows = () => {
  const confirmAction = useConfirm();
  const { t, tSource } = useI18n();
  const uiError = useUiError();
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
      const [w, r] = await Promise.all([adminApi.listWorkflows(true), rbacApi.listRoles()]);
      setWorkflows(w.data);
      // Every role, so a step naming a role that was since deactivated still reads by name.
      setRoles(r.data);
    } catch (err) {
      setError(uiError(err));
    } finally {
      setLoading(false);
    }
  }, [uiError]);

  useEffect(() => {
    void load();
  }, [load]);

  // Who approves, by role name: the chain itself, not only how many steps it has.
  const findRole = useCallback(
    (code: string) => roles.find((item) => item.code.toLowerCase() === code.toLowerCase()),
    [roles],
  );
  const roleLabel = useCallback(
    (code: string) => {
      const role = findRole(code);
      return role ? (role.labelKey ? t(role.labelKey) : role.name) : code;
    },
    [findRole, t],
  );
  // A blank step is saved under its approver's name in the source language (Vietnamese),
  // whatever language the administrator uses: forms collect source-language content only.
  const defaultStepName = useCallback(
    (code: string) => {
      const role = findRole(code);
      const label = role ? (role.labelKey ? tSource(role.labelKey) : role.name) : code;
      return tSource("workflow.stepDefaultName", { role: label });
    },
    [findRole, tSource],
  );
  const activeRoles = useMemo(() => roles.filter((role) => role.isActive), [roles]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return workflows;
    return workflows.filter(
      (w) =>
        w.name.toLowerCase().includes(q) ||
        w.steps.some((step) => roleLabel(step.approverRoleCode).toLowerCase().includes(q)),
    );
  }, [workflows, query, roleLabel]);

  // Why nobody can approve a step of an applied workflow (the quote approval), in words.
  const issueText = (issue: WorkflowStepIssue, stepNumber: number) =>
    t(`workflow.issue.${issue.issue}`, {
      n: stepNumber,
      role: issue.roleLabelKey ? t(issue.roleLabelKey) : roleLabel(issue.roleCode),
    });
  const issueFor = (w: WorkflowConfig | undefined, step: WorkflowStep) =>
    w?.stepIssues?.find(
      (issue) => issue.order === step.order && issue.roleCode === step.approverRoleCode,
    );

  const renderChain = (w: WorkflowConfig) => {
    const steps = [...w.steps].sort((a, b) => a.order - b.order);
    const issues = steps.flatMap((step, index) => {
      const issue = issueFor(w, step);
      return issue ? [{ issue, n: index + 1 }] : [];
    });
    return (
      <div className="space-y-1.5">
        <ol
          className="flex flex-wrap items-center gap-x-1.5 gap-y-1"
          aria-label={t("workflow.field.chain")}
        >
          {steps.map((step, index) => {
            const issue = issueFor(w, step);
            const blocked = issue != null;
            return (
              <li
                key={`${step.order}-${step.approverRoleCode}`}
                className="flex items-center gap-1.5"
              >
                {index > 0 && (
                  <ChevronRight
                    className="h-3.5 w-3.5 shrink-0 text-muted-foreground"
                    aria-hidden
                  />
                )}
                <span
                  className={
                    blocked
                      ? "inline-flex items-center gap-1.5 rounded-md border border-destructive/50 bg-destructive/5 px-2 py-0.5 text-xs"
                      : "inline-flex items-center gap-1.5 rounded-md border bg-muted/40 px-2 py-0.5 text-xs"
                  }
                >
                  {blocked ? (
                    <AlertTriangle className="h-3 w-3 text-destructive" aria-hidden />
                  ) : (
                    <span className="tabular-nums text-muted-foreground">{index + 1}</span>
                  )}
                  <span className="font-medium text-foreground">
                    {issue?.issue === "RoleMissing"
                      ? t("workflow.roleDeleted")
                      : roleLabel(step.approverRoleCode)}
                  </span>
                </span>
              </li>
            );
          })}
        </ol>
        {issues.length > 0 && (
          <ul className="space-y-0.5 text-xs text-destructive" data-testid="wf-step-issues">
            {issues.map(({ issue, n }) => (
              <li key={`${issue.order}-${issue.roleCode}`}>{issueText(issue, n)}</li>
            ))}
          </ul>
        )}
      </div>
    );
  };

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

  // Which business action the form is for: a known one, "other" (codes typed by hand), or
  // none yet. Each business action has one workflow, so the ones in use are offered disabled.
  const [otherBusiness, setOtherBusiness] = useState(false);
  const business = useMemo(() => {
    if (otherBusiness) return OTHER_BUSINESS;
    if (!form.module && !form.action) return "";
    const known = BUSINESS_ACTIONS.find(
      (item) => item.module === form.module && item.action === form.action,
    );
    return known ? businessKeyOf(known.module, known.action) : OTHER_BUSINESS;
  }, [form.module, form.action, otherBusiness]);
  const editingWorkflow = useMemo(
    () => workflows.find((workflow) => workflow.id === editingId),
    [workflows, editingId],
  );
  const takenBusiness = useMemo(
    () =>
      new Set(
        workflows
          .filter((workflow) => workflow.id !== editingId)
          .map((workflow) => businessKeyOf(workflow.module, workflow.action)),
      ),
    [workflows, editingId],
  );
  // A name equal to a business action's label was filled in by the form, so it follows
  // the action; a name the administrator typed is kept.
  const isActionLabel = (name: string) =>
    BUSINESS_ACTIONS.some((item) => [tSource(item.labelKey), t(item.labelKey)].includes(name));
  const chooseBusiness = (value: string) => {
    const nameFor = (prevName: string, label: string) =>
      prevName.trim() && !isActionLabel(prevName) ? prevName : label;
    if (value === OTHER_BUSINESS) {
      setOtherBusiness(true);
      setForm((prev) => ({ ...prev, module: "", action: "", name: nameFor(prev.name, "") }));
      return;
    }
    const known = BUSINESS_ACTIONS.find(
      (item) => businessKeyOf(item.module, item.action) === value,
    );
    if (!known) return;
    setOtherBusiness(false);
    setForm((prev) => ({
      ...prev,
      module: known.module,
      action: known.action,
      // Named after the business action in the source language, unless the user named it.
      name: nameFor(prev.name, tSource(known.labelKey)),
    }));
  };

  const openCreate = () => {
    setEditingId(null);
    setOtherBusiness(false);
    setForm({ ...emptyForm, sortOrder: workflows.length });
    setFormError(null);
    setDialogOpen(true);
  };
  const openEdit = (wf: WorkflowConfig) => {
    setEditingId(wf.id);
    setOtherBusiness(
      !BUSINESS_ACTIONS.some((item) => item.module === wf.module && item.action === wf.action),
    );
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

  // A name that names the previous approver would now be wrong: clear it, so the step is
  // saved under its new approver unless the administrator writes another name.
  const changeStepRole = (index: number, code: string) => {
    setForm((prev) => ({
      ...prev,
      steps: prev.steps.map((step, i) => {
        if (i !== index) return step;
        const previous = step.approverRoleCode ? findRole(step.approverRoleCode) : undefined;
        const previousLabels = previous
          ? [roleLabel(previous.code), previous.labelKey ? tSource(previous.labelKey) : ""]
          : [];
        const namesPrevious = previousLabels.some((label) => label && step.name.includes(label));
        return { ...step, approverRoleCode: code, name: namesPrevious ? "" : step.name };
      }),
    }));
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
        steps: prev.steps.filter((_, i) => i !== index).map((s, i) => ({ ...s, order: i + 1 })),
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
    if (!business) {
      setFormError(t("workflow.businessRequired"));
      return;
    }
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
    if (form.steps.some((s) => !s.approverRoleCode)) {
      setFormError(t("workflow.stepRoleRequired"));
      return;
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
        // An unnamed step reads as its approver, e.g. "Trưởng phòng Cung ứng duyệt".
        name: s.name.trim() || defaultStepName(s.approverRoleCode),
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
      setFormError(uiError(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (wf: WorkflowConfig) => {
    if (!(await confirmAction(t("form.confirmDelete")))) return;
    try {
      await adminApi.deleteWorkflow(wf.id);
      toast({ title: t("form.deleted") });
      await load();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
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
      <div className="space-y-4">
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
                placeholder={t("workflow.searchByNameOrRole")}
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
            <ul className="grid gap-3 xl:hidden">
              {filtered.map((w) => (
                <li key={w.id} className="rounded-lg border bg-card p-3 shadow-sm">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex min-w-0 items-start gap-2">
                      {canManage && (
                        <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                          <Checkbox
                            checked={selectedIds.has(w.id)}
                            onCheckedChange={(v) => toggleOne(w.id, v === true)}
                            aria-label={`${t("common.selectItem")} · ${w.name}`}
                          />
                        </span>
                      )}
                      <div className="min-w-0">
                        <h3 className="break-words text-sm font-semibold leading-tight">
                          {w.name}
                        </h3>
                      </div>
                    </div>
                    {w.isActive ? (
                      <Check
                        className="h-4 w-4 shrink-0 text-success-strong"
                        aria-label={t("workflow.field.isActive")}
                      />
                    ) : (
                      <X
                        className="h-4 w-4 shrink-0 text-muted-foreground"
                        aria-label={t("workflow.field.isActive")}
                      />
                    )}
                  </div>
                  {w.description && (
                    <p className="mt-1.5 break-words text-xs text-muted-foreground">
                      {w.description}
                    </p>
                  )}
                  <div className="mt-2.5">{renderChain(w)}</div>
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
            <div className="hidden overflow-x-auto rounded-lg border xl:block">
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
                    <th className="w-[32%] min-w-[220px] px-3 py-3 text-left font-medium">
                      {t("workflow.field.name")}
                    </th>
                    <th className="min-w-[260px] px-3 py-3 text-left font-medium">
                      {t("workflow.field.chain")}
                    </th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">
                      {t("workflow.field.isActive")}
                    </th>
                    {canManage && (
                      <th className="whitespace-nowrap px-3 py-3 text-right font-medium">
                        {t("common.actions")}
                      </th>
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
                            aria-label={`${t("common.selectItem")} · ${w.name}`}
                          />
                        </td>
                      )}
                      <td className="min-w-[220px] px-3 py-3 font-medium">
                        {w.name}
                        {w.description && (
                          <p className="mt-0.5 max-w-prose text-xs font-normal text-muted-foreground">
                            {w.description}
                          </p>
                        )}
                      </td>
                      <td className="px-3 py-3">{renderChain(w)}</td>
                      <td className="whitespace-nowrap px-3 py-3">
                        {w.isActive ? (
                          <Badge
                            variant="outline"
                            className="border-success/30 bg-success-soft text-success-strong"
                          >
                            <Check className="mr-1 h-3 w-3" />
                            {t("common.on")}
                          </Badge>
                        ) : (
                          <Badge
                            variant="outline"
                            className="border-border bg-muted/30 text-muted-foreground"
                          >
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
              {editingId != null ? t("workflow.editModalTitle") : t("workflow.createModalTitle")}
            </DialogTitle>
            <DialogDescription>{t("workflow.dialogIntro")}</DialogDescription>
          </DialogHeader>

          <div className="space-y-5">
            {/* 1. What is approved */}
            <FieldScope className="space-y-1.5">
              <Label htmlFor="wf-business">{t("workflow.field.business")} *</Label>
              <Select value={business} onValueChange={chooseBusiness}>
                <SelectTrigger id="wf-business" data-testid="wf-business">
                  <SelectValue placeholder={t("workflow.businessPlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {BUSINESS_ACTIONS.map((item) => {
                    const key = businessKeyOf(item.module, item.action);
                    const taken = takenBusiness.has(key);
                    return (
                      <SelectItem key={key} value={key} disabled={taken}>
                        {t(item.labelKey)}
                        {taken ? ` · ${t("workflow.business.taken")}` : ""}
                      </SelectItem>
                    );
                  })}
                  <SelectItem value={OTHER_BUSINESS}>{t("workflow.business.other")}</SelectItem>
                </SelectContent>
              </Select>
              {business === businessKeyOf("quotes", "approve") && (
                <p className="flex gap-2 rounded-md border bg-muted/40 p-2.5 text-xs text-foreground">
                  <Info className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" aria-hidden />
                  {t("workflow.quoteRule")}
                </p>
              )}
            </FieldScope>

            {business === OTHER_BUSINESS && (
              <div className="space-y-2 rounded-md border border-dashed p-3">
                <p className="text-xs font-medium">{t("workflow.technicalCodes")}</p>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="wf-module" className="text-xs">
                      {t("workflow.field.module")} *
                    </Label>
                    <Input
                      id="wf-module"
                      value={form.module}
                      onChange={(e) => setForm({ ...form, module: e.target.value.toLowerCase() })}
                      placeholder="procurement.rfq"
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
                      placeholder="award"
                      className="h-9 font-mono"
                    />
                  </div>
                </div>
                <p className="text-xs text-muted-foreground">{t("workflow.identifierHint")}</p>
              </div>
            )}

            {/* 2. Name and note */}
            <div className="space-y-3">
              <div className="space-y-1.5">
                <Label htmlFor="wf-name">{t("workflow.field.name")} *</Label>
                <Input
                  id="wf-name"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="wf-description">{t("workflow.field.descriptionOptional")}</Label>
                <Textarea
                  id="wf-description"
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                  rows={2}
                />
              </div>
            </div>

            {/* 3. Who approves, in order */}
            <section className="space-y-2" aria-labelledby="wf-steps-title">
              <div className="flex items-center justify-between gap-2">
                <h4 id="wf-steps-title" className="text-sm font-semibold">
                  {t("workflow.stepsTitle")}
                </h4>
                <Button type="button" size="sm" variant="outline" onClick={addStep}>
                  <Plus className="mr-1 h-3.5 w-3.5" /> {t("workflow.addStep")}
                </Button>
              </div>
              <ol className="space-y-2">
                {form.steps.map((step, idx) => (
                  <li
                    key={idx}
                    className="space-y-3 rounded-md border bg-card p-3"
                    data-testid={`wf-step-${idx}`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="inline-flex items-center gap-2 text-sm font-semibold">
                        <span
                          className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-muted text-xs tabular-nums"
                          aria-hidden
                        >
                          {idx + 1}
                        </span>
                        {t("workflow.stepTitle", { n: idx + 1 })}
                      </span>
                      <div className="inline-flex shrink-0 items-center">
                        <Button
                          type="button"
                          size="icon"
                          variant="ghost"
                          onClick={() => moveStep(idx, -1)}
                          disabled={idx === 0}
                          aria-label={t("workflow.moveUp")}
                          className="h-8 w-8"
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
                          className="h-8 w-8"
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
                          className="h-8 w-8 text-destructive hover:text-destructive"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </div>
                    <FieldScope className="space-y-1">
                      <Label className="text-xs">{t("workflow.field.approverRole")} *</Label>
                      <Select
                        value={step.approverRoleCode}
                        onValueChange={(v) => changeStepRole(idx, v)}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={t("workflow.approverPlaceholder")} />
                        </SelectTrigger>
                        <SelectContent>
                          {activeRoles.map((r) => (
                            <SelectItem key={r.code} value={r.code}>
                              {r.labelKey ? t(r.labelKey) : r.name}
                            </SelectItem>
                          ))}
                          {step.approverRoleCode &&
                            !activeRoles.some((r) => r.code === step.approverRoleCode) && (
                              <SelectItem value={step.approverRoleCode}>
                                {roleLabel(step.approverRoleCode)} · {t("workflow.roleInactive")}
                              </SelectItem>
                            )}
                        </SelectContent>
                      </Select>
                      {(() => {
                        // The saved step's problem, while its role is unchanged in the form.
                        const issue = issueFor(editingWorkflow, step);
                        return issue ? (
                          <p role="alert" className="text-xs text-destructive">
                            {issueText(issue, idx + 1)}
                          </p>
                        ) : null;
                      })()}
                    </FieldScope>
                    <div className="grid gap-3 sm:grid-cols-2">
                      <FieldScope className="space-y-1">
                        <Label className="text-xs">{t("workflow.field.stepNameOptional")}</Label>
                        <Input
                          value={step.name}
                          onChange={(e) => patchStep(idx, { name: e.target.value })}
                          placeholder={
                            step.approverRoleCode
                              ? defaultStepName(step.approverRoleCode)
                              : undefined
                          }
                          className="h-9"
                        />
                      </FieldScope>
                      <FieldScope className="space-y-1">
                        <Label className="text-xs">{t("workflow.field.deadlineHours")}</Label>
                        <Input
                          type="number"
                          min={0}
                          inputMode="numeric"
                          value={step.slaHours}
                          onChange={(e) =>
                            patchStep(idx, { slaHours: Number(e.target.value) || 0 })
                          }
                          className="h-9"
                        />
                        <p className="text-xs text-muted-foreground">
                          {t("workflow.deadlineHint")}
                        </p>
                      </FieldScope>
                      <FieldScope className="flex items-start gap-2 sm:col-span-2">
                        <FieldControl>
                          <Checkbox
                            checked={step.requireAllApprovers}
                            onCheckedChange={(v) =>
                              patchStep(idx, { requireAllApprovers: v === true })
                            }
                            className="mt-0.5"
                          />
                        </FieldControl>
                        <Label className="text-xs font-normal leading-snug">
                          {t("workflow.field.everyoneMustApprove")}
                        </Label>
                      </FieldScope>
                    </div>
                  </li>
                ))}
              </ol>
            </section>

            {/* 4. In use */}
            <div className="flex items-start gap-3 rounded-md border p-3">
              <Switch
                id="wf-active"
                checked={form.isActive}
                onCheckedChange={(checked) => setForm({ ...form, isActive: checked })}
              />
              <div className="space-y-0.5">
                <Label htmlFor="wf-active" className="text-sm font-medium">
                  {t("workflow.field.activeNow")}
                </Label>
                <p className="text-xs text-muted-foreground">{t("workflow.activeHint")}</p>
              </div>
            </div>

            {formError && (
              <p role="alert" className="text-sm text-destructive">
                {formError}
              </p>
            )}
          </div>

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={saving}>
              {saving ? t("common.saving") : t("workflow.saveWorkflow")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default Workflows;
