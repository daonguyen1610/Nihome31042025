import { FormEvent, useCallback, useEffect, useState } from "react";
import { CircleDollarSign, Plus, Send, Trash2 } from "lucide-react";
import { ConstructionWorkspaceShell } from "./ConstructionWorkspaceShell";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
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
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ConstructionQsBatchLineRequest,
  type ConstructionQsBatchResponse,
  type ConstructionQsDirection,
} from "@/services/adminApi";

const emptyLine = (): ConstructionQsBatchLineRequest => ({
  itemCode: "",
  description: "",
  unit: "",
  quantity: 0,
  unitPrice: 0,
});

export default function QsWorkspacePage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [projectId, setProjectId] = useState<number | null>(null);
  const selectProject = useCallback((id: number) => setProjectId(id), []);
  const [rows, setRows] = useState<ConstructionQsBatchResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    direction: "Upstream" as ConstructionQsDirection,
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
    if (projectId == null) return;
    let active = true;
    setLoading(true);
    setError(null);
    void adminApi
      .listConstructionQsBatches(projectId)
      .then(({ data }) => active && setRows(data))
      .catch((cause) => active && setError(extractApiError(cause, t("construction.qs.loadError"))))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, t]);

  const updateLine = (index: number, patch: Partial<ConstructionQsBatchLineRequest>) =>
    setForm((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) =>
        lineIndex === index ? { ...line, ...patch } : line,
      ),
    }));
  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (projectId == null) return;
    setSaving(true);
    try {
      await adminApi.createConstructionQsBatch(projectId, {
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
        supersedesBatchId: null,
        reversesBatchId: null,
      });
      setOpen(false);
      setReload((value) => value + 1);
      toast({ title: t("construction.qs.created") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.qs.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };
  const submit = async (row: ConstructionQsBatchResponse) => {
    if (projectId == null) return;
    try {
      await adminApi.submitConstructionQsBatch(projectId, row.id, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.common.submitted") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.common.actionError")),
      });
    }
  };

  return (
    <ConstructionWorkspaceShell
      title={t("construction.qs.title")}
      description={t("construction.qs.description")}
      projectId={projectId}
      onProjectChange={selectProject}
      testId="qs-page"
    >
      <section className="space-y-4">
        <div className="flex justify-end">
          {has(ADMIN_PERMS.constructionQsCreate) && (
            <Button onClick={() => setOpen(true)} data-testid="qs-new">
              <Plus className="mr-2 h-4 w-4" />
              {t("construction.qs.new")}
            </Button>
          )}
        </div>
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
        ) : rows.length === 0 ? (
          <PageEmpty message={t("construction.qs.empty")} />
        ) : (
          <div className="grid gap-3 lg:grid-cols-2">
            {rows.map((row) => (
              <article
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
                  <Badge variant="outline">{t(`construction.status.${row.status}`)}</Badge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.qs.contract")}</dt>
                    <dd>#{row.contractId}</dd>
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
                {row.status === "Draft" && (
                  <Button
                    size="sm"
                    onClick={() => void submit(row)}
                    data-testid={`qs-submit-${row.id}`}
                  >
                    <Send className="mr-2 h-4 w-4" />
                    {t("construction.common.submit")}
                  </Button>
                )}
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
                {t("construction.qs.new")}
              </DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <div className="space-y-1.5">
                <Label>{t("construction.qs.direction")}</Label>
                <Select
                  value={form.direction}
                  onValueChange={(value) =>
                    setForm({ ...form, direction: value as ConstructionQsDirection })
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
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-contract">{t("construction.qs.contract")}</Label>
                <Input
                  id="qs-contract"
                  type="number"
                  min="1"
                  required
                  value={form.contractId}
                  onChange={(e) => setForm({ ...form, contractId: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="qs-wbs">{t("construction.common.wbs")}</Label>
                <Input
                  id="qs-wbs"
                  required
                  maxLength={100}
                  value={form.wbsCode}
                  onChange={(e) => setForm({ ...form, wbsCode: e.target.value })}
                />
              </div>
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
                  value={form.certifiedAmount}
                  onChange={(value) => setForm({ ...form, certifiedAmount: value })}
                />
              </div>
              <div className="space-y-1.5 sm:col-span-2 lg:col-span-3">
                <Label htmlFor="qs-description">{t("construction.qs.batchDescription")}</Label>
                <Textarea
                  id="qs-description"
                  required
                  maxLength={2000}
                  value={form.description}
                  onChange={(e) => setForm({ ...form, description: e.target.value })}
                />
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
                    required
                    maxLength={80}
                    placeholder={t("construction.qs.itemCode")}
                    value={line.itemCode}
                    onChange={(e) => updateLine(index, { itemCode: e.target.value })}
                  />
                  <Input
                    required
                    maxLength={500}
                    className="lg:col-span-2"
                    placeholder={t("construction.qs.lineDescription")}
                    value={line.description}
                    onChange={(e) => updateLine(index, { description: e.target.value })}
                  />
                  <Input
                    required
                    maxLength={50}
                    placeholder={t("construction.resource.unit")}
                    value={line.unit}
                    onChange={(e) => updateLine(index, { unit: e.target.value })}
                  />
                  <MoneyInput
                    decimalScale={6}
                    value={line.quantity}
                    onChange={(value) => updateLine(index, { quantity: value })}
                  />
                  <div className="flex gap-2">
                    <MoneyInput
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
              />
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
    </ConstructionWorkspaceShell>
  );
}
