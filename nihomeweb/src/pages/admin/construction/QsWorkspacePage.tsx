import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { useLocation, useSearchParams } from "react-router-dom";
import { CircleDollarSign, Pencil, Plus, Send, Trash2 } from "lucide-react";
import { ConstructionReviewActions } from "./ConstructionReviewActions";
import { ConstructionDeletionControl } from "./ConstructionDeletionControl";
import { type ScopeProps, useConstructionActions } from "./constructionScope";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { REVIEW_STATUS_TONE } from "./constructionStatus";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MoneyInput } from "@/components/ui/money-input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConstructionQsBatchLineRequest,
  type ConstructionQsBatchResponse,
  type ConstructionQsAdjustmentKind,
  type ConstructionQsDirection,
  type ConstructionQsReferenceDataResponse,
} from "@/services/adminApi";

const emptyLine = (): ConstructionQsBatchLineRequest => ({
  itemCode: "",
  description: "",
  unit: "",
  quantity: 0,
  unitPrice: 0,
});

export function QsBatchesPanel({ projectId, scope = "execute" }: ScopeProps) {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { canManage, canApprove, canEditRow, canDeleteRow } = useConstructionActions(scope, {
    manage: ADMIN_PERMS.constructionQsCreate,
    supervisor: ADMIN_PERMS.constructionQsReview,
    approve: ADMIN_PERMS.constructionQsReview,
  });
  const [searchParams, setSearchParams] = useSearchParams();
  const { hash } = useLocation();
  const scrollToSource = useCallback(
    (node: HTMLElement | null) => {
      if (node && hash === `#${node.id}`) node.scrollIntoView({ block: "center" });
    },
    [hash],
  );
  const requestedContract = Number(searchParams.get("contractId"));
  const contractFilter =
    Number.isInteger(requestedContract) && requestedContract > 0 ? requestedContract : null;
  const [rows, setRows] = useState<ConstructionQsBatchResponse[]>([]);
  const visibleRows = rows.filter(
    (row) => contractFilter == null || row.contractId === contractFilter,
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ConstructionQsBatchResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [referenceData, setReferenceData] = useState<ConstructionQsReferenceDataResponse>({
    contracts: [],
    approvedBoqRevisions: [],
  });
  const [lookupsLoading, setLookupsLoading] = useState(false);
  const [form, setForm] = useState({
    direction: "Upstream" as ConstructionQsDirection,
    adjustmentKind: "Original" as ConstructionQsAdjustmentKind,
    predecessorBatchId: "",
    contractId: "",
    wbsCode: "",
    periodFrom: "",
    periodTo: "",
    projectBoqRevisionId: "",
    contractAppendixId: "",
    contractPaymentMilestoneId: "",
    certifiedAmount: 0,
    description: "",
    evidenceDocumentIds: "",
    lines: [emptyLine()],
  });

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void adminApi
      .listConstructionQsBatches(projectId)
      .then(({ data }) => active && setRows(data))
      .catch((cause) => active && setError(uiError(cause, t("construction.qs.loadError"))))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, t, uiError]);

  useEffect(() => {
    let active = true;
    setLookupsLoading(true);
    void adminApi
      .getConstructionQsReferenceData(projectId)
      .then(({ data }) => {
        if (!active) return;
        setReferenceData(data);
      })
      .catch((cause) => {
        if (active)
          toast({
            variant: "destructive",
            title: uiError(cause, t("construction.qs.loadError")),
          });
      })
      .finally(() => active && setLookupsLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, t, toast, uiError]);

  const updateLine = (index: number, patch: Partial<ConstructionQsBatchLineRequest>) =>
    setForm((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) =>
        lineIndex === index ? { ...line, ...patch } : line,
      ),
    }));
  const emptyForm = () => ({
    direction: "Upstream" as ConstructionQsDirection,
    adjustmentKind: "Original" as ConstructionQsAdjustmentKind,
    predecessorBatchId: "",
    contractId: "",
    wbsCode: "",
    periodFrom: "",
    periodTo: "",
    projectBoqRevisionId: "",
    contractAppendixId: "",
    contractPaymentMilestoneId: "",
    certifiedAmount: 0,
    description: "",
    evidenceDocumentIds: "",
    lines: [emptyLine()],
  });
  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setOpen(true);
  };
  const openEdit = (row: ConstructionQsBatchResponse) => {
    setEditing(row);
    setForm({
      direction: row.direction,
      adjustmentKind: row.adjustmentKind,
      predecessorBatchId: String(row.supersedesBatchId ?? row.reversesBatchId ?? ""),
      contractId: String(row.contractId),
      wbsCode: row.wbsCode,
      periodFrom: row.periodFrom.slice(0, 10),
      periodTo: row.periodTo.slice(0, 10),
      projectBoqRevisionId: row.projectBoqRevisionId ? String(row.projectBoqRevisionId) : "",
      contractAppendixId: row.contractAppendixId ? String(row.contractAppendixId) : "",
      contractPaymentMilestoneId: row.contractPaymentMilestoneId
        ? String(row.contractPaymentMilestoneId)
        : "",
      certifiedAmount: row.certifiedAmount,
      description: row.description,
      evidenceDocumentIds: row.evidenceDocumentIds.join(","),
      lines:
        row.lines.length > 0
          ? row.lines.map((line) => ({
              itemCode: line.itemCode,
              description: line.description,
              unit: line.unit,
              quantity: line.quantity,
              unitPrice: line.unitPrice,
            }))
          : [emptyLine()],
    });
    setOpen(true);
  };
  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (!form.contractId) {
      toast({ variant: "destructive", title: t("construction.qs.contractRequired") });
      return;
    }
    if (form.direction === "Upstream" && !form.contractPaymentMilestoneId) {
      toast({ variant: "destructive", title: t("construction.qs.milestoneRequired") });
      return;
    }
    if (form.adjustmentKind !== "Original" && !form.predecessorBatchId) {
      toast({ variant: "destructive", title: t("construction.qs.predecessorRequired") });
      return;
    }
    const body = {
      direction: form.direction,
      contractId: Number(form.contractId),
      wbsCode: form.wbsCode.trim(),
      periodFrom: form.periodFrom,
      periodTo: form.periodTo,
      projectBoqRevisionId: form.projectBoqRevisionId ? Number(form.projectBoqRevisionId) : null,
      contractAppendixId: form.contractAppendixId ? Number(form.contractAppendixId) : null,
      contractPaymentMilestoneId: form.contractPaymentMilestoneId
        ? Number(form.contractPaymentMilestoneId)
        : null,
      certifiedAmount: form.certifiedAmount,
      description: form.description.trim(),
      lines: form.lines,
      evidenceDocumentIds: form.evidenceDocumentIds
        .split(",")
        .map((value) => value.trim())
        .filter(Boolean)
        .map(Number),
      supersedesBatchId:
        form.adjustmentKind === "SupersedingCorrection" ? Number(form.predecessorBatchId) : null,
      reversesBatchId: form.adjustmentKind === "Reversal" ? Number(form.predecessorBatchId) : null,
    };
    setSaving(true);
    try {
      if (editing) {
        await adminApi.updateConstructionQsBatch(projectId, editing.id, {
          ...body,
          rowVersion: editing.rowVersion,
        });
        toast({ title: t("construction.common.updated") });
      } else {
        await adminApi.createConstructionQsBatch(projectId, body);
        toast({ title: t("construction.qs.created") });
      }
      setOpen(false);
      setEditing(null);
      setReload((value) => value + 1);
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.qs.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };

  const submit = async (row: ConstructionQsBatchResponse) => {
    try {
      await adminApi.submitConstructionQsBatch(projectId, row.id, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.common.submitted") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
    }
  };

  const decide = async (row: ConstructionQsBatchResponse, approved: boolean, reason: string) => {
    try {
      await adminApi.decideConstructionQsBatch(projectId, row.id, approved, reason, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.cm.decisionSaved") });
      return true;
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
      return false;
    }
  };

  return (
    <>
      <section className="space-y-4">
        {contractFilter != null && (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-muted/30 p-3 text-sm">
            <span>
              {t("construction.qs.contract")}{" "}
              {referenceData.contracts.find((contract) => contract.id === contractFilter)
                ?.contractNumber ?? `#${contractFilter}`}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() =>
                setSearchParams(
                  (current) => {
                    const next = new URLSearchParams(current);
                    next.delete("contractId");
                    return next;
                  },
                  { replace: true },
                )
              }
            >
              {t("common.reset")}
            </Button>
          </div>
        )}
        <div className="flex justify-end">
          {canManage && (
            <Button onClick={openCreate} data-testid="qs-new">
              <Plus className="mr-2 h-4 w-4" />
              {t("construction.qs.new")}
            </Button>
          )}
        </div>
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
        ) : visibleRows.length === 0 ? (
          <PageEmpty
            message={t(contractFilter != null ? "common.noResults" : "construction.qs.empty")}
            action={
              contractFilter != null
                ? undefined
                : canManage && (
                    <Button onClick={openCreate} data-testid="qs-empty-new">
                      <Plus className="mr-2 h-4 w-4" />
                      {t("construction.qs.new")}
                    </Button>
                  )
            }
          />
        ) : (
          <div className="grid gap-3 lg:grid-cols-2">
            {visibleRows.map((row) => (
              <article
                ref={scrollToSource}
                id={`qs-${row.id}`}
                key={row.id}
                className="space-y-3 rounded-xl border bg-card p-4 shadow-sm"
                data-testid={`qs-row-${row.id}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{row.code}</p>
                    <p className="text-sm text-muted-foreground">
                      {t(`construction.qs.direction.${row.direction}`)}
                    </p>
                  </div>
                  <ConstructionStatusBadge tone={REVIEW_STATUS_TONE[row.status]}>
                    {t(`construction.status.${row.status}`)}
                  </ConstructionStatusBadge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qs.contract")}</dt>
                    <dd>
                      {referenceData.contracts.find((contract) => contract.id === row.contractId)
                        ?.contractNumber ?? `#${row.contractId}`}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.common.wbs")}</dt>
                    <dd>{row.wbsCode}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qs.amount")}</dt>
                    <dd className="font-medium tabular-nums">
                      {row.certifiedAmount.toLocaleString(lang)}
                    </dd>
                  </div>
                  <div className="col-span-2 sm:col-span-3">
                    <dt className="text-muted-foreground">{t("construction.qs.period")}</dt>
                    <dd>
                      {new Date(row.periodFrom).toLocaleDateString(lang)} –{" "}
                      {new Date(row.periodTo).toLocaleDateString(lang)}
                    </dd>
                  </div>
                </dl>
                <p className="text-sm">{row.description}</p>
                {row.status === "Draft" && canEditRow(row.createdByUserId) && (
                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      onClick={() => void submit(row)}
                      data-testid={`qs-submit-${row.id}`}
                    >
                      <Send className="mr-2 h-4 w-4" />
                      {t("construction.common.submit")}
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => openEdit(row)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      {t("common.edit")}
                    </Button>
                  </div>
                )}
                {(row.status === "Draft" || row.status === "CmRejected") &&
                  canDeleteRow(row.createdByUserId) && (
                    <ConstructionDeletionControl
                      rowVersion={row.rowVersion}
                      getImpact={() =>
                        adminApi.getConstructionQsBatchDeletionImpact(projectId, row.id)
                      }
                      deleteRecord={(request) =>
                        adminApi.deleteConstructionQsBatch(projectId, row.id, request)
                      }
                      onDeleted={() => setReload((value) => value + 1)}
                    />
                  )}
                <ConstructionReviewActions
                  allowed={canApprove}
                  status={row.status}
                  createdByUserId={row.createdByUserId}
                  lastEditedByUserId={row.lastEditedByUserId}
                  onDecide={(approved, reason) => decide(row, approved, reason)}
                />
              </article>
            ))}
          </div>
        )}
      </section>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-3xl">
          <form onSubmit={save} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <CircleDollarSign className="h-5 w-5" />
                {t(editing ? "common.edit" : "construction.qs.new")}
              </DialogTitle>
            </DialogHeader>
            <p className="text-sm text-muted-foreground">{t("construction.qs.formIntro")}</p>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <FieldScope className="space-y-1.5">
                <Label>{t("construction.qs.direction")}</Label>
                <Select
                  value={form.direction}
                  onValueChange={(value) =>
                    setForm({
                      ...form,
                      direction: value as ConstructionQsDirection,
                      adjustmentKind: "Original",
                      predecessorBatchId: "",
                      contractId: "",
                      contractAppendixId: "",
                      contractPaymentMilestoneId: "",
                    })
                  }
                >
                  <SelectTrigger data-testid="qs-direction">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Upstream">
                      {t("construction.qs.direction.Upstream")}
                    </SelectItem>
                    <SelectItem value="Downstream">
                      {t("construction.qs.direction.Downstream")}
                    </SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  {t("construction.qs.directionHint")}
                </p>
              </FieldScope>
              <div className="space-y-1.5">
                <Label htmlFor="qs-adjustment">{t("construction.qs.adjustmentMode")}</Label>
                <Select
                  value={form.adjustmentKind}
                  onValueChange={(value) =>
                    setForm({
                      ...form,
                      adjustmentKind: value as ConstructionQsAdjustmentKind,
                      predecessorBatchId: "",
                    })
                  }
                >
                  <SelectTrigger id="qs-adjustment" data-testid="qs-adjustment-mode">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {(["Original", "SupersedingCorrection", "Reversal"] as const).map((kind) => (
                      <SelectItem key={kind} value={kind}>
                        {t(`construction.qs.adjustment.${kind}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {form.adjustmentKind !== "Original" && (
                <div className="space-y-1.5">
                  <Label htmlFor="qs-predecessor">{t("construction.qs.predecessor")}</Label>
                  <Select
                    required
                    value={form.predecessorBatchId}
                    onValueChange={(value) => setForm({ ...form, predecessorBatchId: value })}
                  >
                    <SelectTrigger id="qs-predecessor" data-testid="qs-predecessor">
                      <SelectValue placeholder={t("construction.qs.predecessor")} />
                    </SelectTrigger>
                    <SelectContent>
                      {rows
                        .filter(
                          (row) =>
                            row.direction === form.direction &&
                            row.status === "CmApproved" &&
                            row.recordState === "Active",
                        )
                        .map((row) => (
                          <SelectItem key={row.id} value={String(row.id)}>
                            {row.code} · {row.certifiedAmount.toLocaleString(lang)}
                          </SelectItem>
                        ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              <div className="space-y-1.5">
                <Label htmlFor="qs-contract">{t("construction.qs.contract")}</Label>
                <Select
                  required
                  value={form.contractId}
                  onValueChange={(value) =>
                    setForm({
                      ...form,
                      contractId: value,
                      contractAppendixId: "",
                      contractPaymentMilestoneId: "",
                    })
                  }
                  disabled={lookupsLoading}
                >
                  <SelectTrigger id="qs-contract" data-testid="qs-contract">
                    <SelectValue placeholder={t("construction.qs.selectContract")} />
                  </SelectTrigger>
                  <SelectContent>
                    {referenceData.contracts
                      .filter((contract) => contract.direction === form.direction)
                      .map((contract) => (
                        <SelectItem key={contract.id} value={String(contract.id)}>
                          {contract.contractNumber}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">{t("construction.qs.contractHint")}</p>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-wbs">{t("construction.common.wbs")}</Label>
                <Input
                  id="qs-wbs"
                  required
                  maxLength={100}
                  placeholder="WBS-3.2-MEP"
                  aria-describedby="qs-wbs-hint"
                  value={form.wbsCode}
                  onChange={(e) => setForm({ ...form, wbsCode: e.target.value })}
                />
                <p id="qs-wbs-hint" className="text-xs text-muted-foreground">
                  {t("construction.qs.wbsHint")}
                </p>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-boq">{t("procurement.boq.title")}</Label>
                <Select
                  value={form.projectBoqRevisionId || "none"}
                  onValueChange={(value) =>
                    setForm({ ...form, projectBoqRevisionId: value === "none" ? "" : value })
                  }
                >
                  <SelectTrigger id="qs-boq" data-testid="qs-boq-revision">
                    <SelectValue placeholder={t("construction.qs.notApplicable")} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">{t("construction.qs.notApplicable")}</SelectItem>
                    {referenceData.approvedBoqRevisions.map((revision) => (
                      <SelectItem key={revision.id} value={String(revision.id)}>
                        BOQ v{revision.revisionNumber} · {revision.currency}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-vo">{t("contracts.appendix.title")}</Label>
                <Select
                  value={form.contractAppendixId || "none"}
                  onValueChange={(value) =>
                    setForm({ ...form, contractAppendixId: value === "none" ? "" : value })
                  }
                  disabled={!form.contractId}
                >
                  <SelectTrigger id="qs-vo" data-testid="qs-contract-appendix">
                    <SelectValue placeholder={t("construction.qs.notApplicable")} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">{t("construction.qs.notApplicable")}</SelectItem>
                    {(
                      referenceData.contracts.find(
                        (contract) => contract.id === Number(form.contractId),
                      )?.approvedAppendices ?? []
                    ).map((appendix) => (
                      <SelectItem key={appendix.id} value={String(appendix.id)}>
                        VO-{appendix.voNumber} · {appendix.title}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {form.direction === "Upstream" && (
                <div className="space-y-1.5">
                  <Label htmlFor="qs-milestone">{t("contracts.milestonesTitle")}</Label>
                  <Select
                    required
                    value={form.contractPaymentMilestoneId}
                    onValueChange={(value) =>
                      setForm({ ...form, contractPaymentMilestoneId: value })
                    }
                    disabled={!form.contractId}
                  >
                    <SelectTrigger id="qs-milestone" data-testid="qs-payment-milestone">
                      <SelectValue placeholder={t("construction.qs.selectMilestone")} />
                    </SelectTrigger>
                    <SelectContent>
                      {(
                        referenceData.contracts.find(
                          (contract) => contract.id === Number(form.contractId),
                        )?.paymentMilestones ?? []
                      ).map((milestone) => (
                        <SelectItem key={milestone.id} value={String(milestone.id)}>
                          {milestone.order}. {milestone.name} ·{" "}
                          {milestone.amount.toLocaleString(lang)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <p className="text-xs text-muted-foreground">
                    {t("construction.qs.milestoneHint")}
                  </p>
                </div>
              )}
              <div className="space-y-1.5">
                <Label htmlFor="qs-from">{t("construction.qs.periodFrom")}</Label>
                <Input
                  id="qs-from"
                  type="date"
                  required
                  value={form.periodFrom}
                  onChange={(e) => setForm({ ...form, periodFrom: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-to">{t("construction.qs.periodTo")}</Label>
                <Input
                  id="qs-to"
                  type="date"
                  required
                  min={form.periodFrom}
                  value={form.periodTo}
                  onChange={(e) => setForm({ ...form, periodTo: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-amount">{t("construction.qs.amount")}</Label>
                <MoneyInput
                  id="qs-amount"
                  aria-describedby="qs-amount-hint"
                  value={form.certifiedAmount}
                  onChange={(value) => setForm({ ...form, certifiedAmount: value })}
                />
                <p id="qs-amount-hint" className="text-xs text-muted-foreground">
                  {t("construction.qs.amountHint")}
                </p>
              </div>
              <div className="space-y-1.5 sm:col-span-2 lg:col-span-3">
                <Label htmlFor="qs-description">{t("construction.qs.batchDescription")}</Label>
                <Textarea
                  id="qs-description"
                  required
                  maxLength={2000}
                  placeholder={t("construction.qs.descriptionPlaceholder")}
                  aria-describedby="qs-description-hint"
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
                <p id="qs-description-hint" className="text-xs text-muted-foreground">
                  {t("construction.qs.descriptionHint")}
                </p>
              </div>
            </div>
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-medium">{t("construction.qs.lines")}</h3>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setForm({ ...form, lines: [...form.lines, emptyLine()] })}
                >
                  <Plus className="mr-2 h-4 w-4" />
                  {t("construction.qs.addLine")}
                </Button>
              </div>
              {form.lines.map((line, index) => (
                <div
                  key={index}
                  className="grid gap-3 rounded-lg border p-3 sm:grid-cols-2 lg:grid-cols-6"
                >
                  <Input
                    aria-label={t("construction.qs.itemCode")}
                    required
                    maxLength={80}
                    placeholder={t("construction.qs.itemCode")}
                    value={line.itemCode}
                    onChange={(e) => updateLine(index, { itemCode: e.target.value })}
                  />
                  <Input
                    aria-label={t("construction.qs.lineDescription")}
                    required
                    maxLength={500}
                    className="lg:col-span-2"
                    placeholder={t("construction.qs.lineDescription")}
                    value={line.description}
                    onChange={(e) => updateLine(index, { description: e.target.value })}
                  />
                  <Input
                    aria-label={t("construction.resource.unit")}
                    required
                    maxLength={50}
                    placeholder={t("construction.resource.unit")}
                    value={line.unit}
                    onChange={(e) => updateLine(index, { unit: e.target.value })}
                  />
                  <MoneyInput
                    aria-label={t("quotes.boq.qty")}
                    decimalScale={6}
                    value={line.quantity}
                    onChange={(value) => updateLine(index, { quantity: value })}
                  />
                  <div className="flex gap-2">
                    <MoneyInput
                      aria-label={t("quotes.boq.unitPrice")}
                      decimalScale={4}
                      value={line.unitPrice}
                      onChange={(value) => updateLine(index, { unitPrice: value })}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      disabled={form.lines.length === 1}
                      onClick={() =>
                        setForm({
                          ...form,
                          lines: form.lines.filter((_, lineIndex) => lineIndex !== index),
                        })
                      }
                      aria-label={t("common.delete")}
                    >
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="qs-evidence">{t("construction.qaQc.evidenceIds")}</Label>
              <Input
                id="qs-evidence"
                value={form.evidenceDocumentIds}
                onChange={(e) => setForm({ ...form, evidenceDocumentIds: e.target.value })}
                placeholder="101, 102"
                aria-describedby="qs-evidence-hint"
              />
              <p id="qs-evidence-hint" className="text-xs text-muted-foreground">
                {t("construction.qs.evidenceHint")}
              </p>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={saving} data-testid="qs-save">
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
