import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
import { FormEvent, useEffect, useState } from "react";
import { personOption, type PersonIdentity } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Boxes, Pencil, Plus, Send } from "lucide-react";
import { ConstructionReviewActions } from "./ConstructionReviewActions";
import { ConstructionDeletionControl } from "./ConstructionDeletionControl";
import { type ScopeProps, useConstructionActions } from "./constructionScope";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { REVIEW_STATUS_TONE } from "./constructionStatus";
import { ProjectWbsField } from "./ProjectWbsField";
import { usePublishedProjectWbs } from "./usePublishedProjectWbs";
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
  type ConstructionResourceRequestResponse,
  type ConstructionResourceType,
  type ConstructionResourceReferenceDataResponse,
} from "@/services/adminApi";
import { formatDate } from "@/lib/displaySettings";

const TYPES: ConstructionResourceType[] = ["Material", "Equipment", "Labour"];
type UserOption = PersonIdentity & { userId: number; userName: string };
type BoqLineOption = ConstructionResourceReferenceDataResponse["boqLines"][number];

export function ResourceRequestsPanel({ projectId, scope = "execute" }: ScopeProps) {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { canManage, canApprove, canEditRow, canDeleteRow } = useConstructionActions(scope, {
    manage: ADMIN_PERMS.constructionResourceRequestsCreate,
    supervisor: ADMIN_PERMS.constructionResourceRequestsReview,
    approve: ADMIN_PERMS.constructionResourceRequestsReview,
  });
  const [rows, setRows] = useState<ConstructionResourceRequestResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ConstructionResourceRequestResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [siteUsers, setSiteUsers] = useState<UserOption[]>([]);
  const [procurementUsers, setProcurementUsers] = useState<UserOption[]>([]);
  const [boqLines, setBoqLines] = useState<BoqLineOption[]>([]);
  const {
    items: wbsItems,
    loading: wbsLoading,
    failed: wbsFailed,
    retry: retryWbs,
  } = usePublishedProjectWbs(projectId);
  const [form, setForm] = useState({
    type: "Material" as ConstructionResourceType,
    projectScheduleNodeId: "",
    wbsCode: "",
    needByDate: "",
    quantity: "",
    unit: "",
    reason: "",
    projectBoqLineId: "",
    responsibleSiteUserId: "",
    assignedProcurementUserId: "",
  });

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.listConstructionResourceRequests(projectId),
      adminApi.getConstructionResourceReferenceData(projectId),
    ])
      .then(([requests, references]) => {
        if (!active) return;
        setRows(requests.data);
        setSiteUsers(references.data.siteUsers);
        setProcurementUsers(references.data.procurementUsers);
        setBoqLines(references.data.boqLines);
      })
      .catch((cause) => active && setError(uiError(cause, t("construction.resource.loadError"))))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, t, uiError]);

  const emptyForm = () => ({
    type: "Material" as ConstructionResourceType,
    projectScheduleNodeId: "",
    wbsCode: "",
    needByDate: "",
    quantity: "",
    unit: "",
    reason: "",
    projectBoqLineId: "",
    responsibleSiteUserId: "",
    assignedProcurementUserId: "",
  });

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setOpen(true);
  };

  const openEdit = (row: ConstructionResourceRequestResponse) => {
    setEditing(row);
    setForm({
      type: row.type,
      projectScheduleNodeId: row.projectScheduleNodeId ?? "",
      wbsCode: row.wbsCode,
      needByDate: row.needByDate.slice(0, 10),
      quantity: String(row.quantity),
      unit: row.unit,
      reason: row.reason,
      projectBoqLineId: row.projectBoqLineId ? String(row.projectBoqLineId) : "",
      responsibleSiteUserId: row.responsibleSiteUserId ? String(row.responsibleSiteUserId) : "",
      assignedProcurementUserId: row.assignedProcurementUserId
        ? String(row.assignedProcurementUserId)
        : "",
    });
    setOpen(true);
  };

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const selectedWbs = wbsItems.find((item) => item.nodeId === form.projectScheduleNodeId);
    const keepsLegacyWbs =
      Boolean(editing && !editing.projectScheduleNodeId) &&
      form.wbsCode.trim() === editing?.wbsCode.trim();
    if (!selectedWbs && !keepsLegacyWbs) {
      toast({ variant: "destructive", title: t("construction.common.wbsRequired") });
      return;
    }
    if (
      form.type === "Material" &&
      (!form.projectBoqLineId || !form.responsibleSiteUserId || !form.assignedProcurementUserId)
    ) {
      toast({
        variant: "destructive",
        title: t("construction.resource.materialReferencesRequired"),
      });
      return;
    }
    const body = {
      type: form.type,
      projectScheduleNodeId: form.projectScheduleNodeId || null,
      wbsCode: selectedWbs?.wbsCode ?? editing?.wbsCode ?? "",
      needByDate: form.needByDate,
      quantity: Number(form.quantity),
      unit: form.unit.trim(),
      reason: form.reason.trim(),
      projectBoqLineId: form.projectBoqLineId ? Number(form.projectBoqLineId) : null,
      responsibleSiteUserId: form.responsibleSiteUserId ? Number(form.responsibleSiteUserId) : null,
      assignedProcurementUserId: form.assignedProcurementUserId
        ? Number(form.assignedProcurementUserId)
        : null,
    };
    setSaving(true);
    try {
      if (editing) {
        await adminApi.updateConstructionResourceRequest(projectId, editing.id, {
          ...body,
          rowVersion: editing.rowVersion,
        });
        toast({ title: t("construction.common.updated") });
      } else {
        await adminApi.createConstructionResourceRequest(projectId, body);
        toast({ title: t("construction.resource.created") });
      }
      setOpen(false);
      setEditing(null);
      setForm(emptyForm());
      setReload((value) => value + 1);
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.resource.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };

  const submit = async (row: ConstructionResourceRequestResponse) => {
    try {
      await adminApi.submitConstructionResourceRequest(projectId, row.id, row.rowVersion);
      setReload((value) => value + 1);
      toast({ title: t("construction.common.submitted") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
    }
  };

  const decide = async (
    row: ConstructionResourceRequestResponse,
    approved: boolean,
    reason: string,
  ) => {
    try {
      await adminApi.decideConstructionResourceRequest(
        projectId,
        row.id,
        approved,
        reason,
        row.rowVersion,
      );
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
        {canManage && rows.length > 0 && (
          <div className="flex flex-wrap items-center justify-end gap-2">
            <Button onClick={openCreate} data-testid="resource-new">
              <Plus className="mr-2 h-4 w-4" />
              {t("construction.resource.new")}
            </Button>
          </div>
        )}
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("construction.resource.empty")}
            action={
              canManage && (
                <Button onClick={openCreate} data-testid="resource-empty-new">
                  <Plus className="mr-2 h-4 w-4" />
                  {t("construction.resource.new")}
                </Button>
              )
            }
          />
        ) : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {rows.map((row) => (
              <article
                key={row.id}
                className="space-y-3 rounded-xl border bg-card p-4 shadow-sm"
                data-testid={`resource-row-${row.id}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold">{row.code}</p>
                    <p className="text-sm text-muted-foreground">
                      {t(`construction.resource.type.${row.type}`)}
                    </p>
                  </div>
                  <ConstructionStatusBadge tone={REVIEW_STATUS_TONE[row.status]}>
                    {t(`construction.status.${row.status}`)}
                  </ConstructionStatusBadge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.common.wbs")}</dt>
                    <dd>{row.wbsCode}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.resource.needBy")}</dt>
                    <dd>{formatDate(row.needByDate, lang)}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.resource.quantity")}</dt>
                    <dd>
                      {row.quantity.toLocaleString(lang)} {row.unit}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.resource.handoff")}</dt>
                    <dd>
                      {t(
                        `construction.handoff.${row.suppliedAt ? "Supplied" : row.procurementContractId ? "ContractLinked" : row.handoffStatus}`,
                      )}
                    </dd>
                  </div>
                </dl>
                <p className="text-sm">{row.reason}</p>
                {row.status === "Draft" && canEditRow(row.createdByUserId) && (
                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      size="sm"
                      onClick={() => void submit(row)}
                      data-testid={`resource-submit-${row.id}`}
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
                        adminApi.getConstructionResourceRequestDeletionImpact(projectId, row.id)
                      }
                      deleteRecord={(request) =>
                        adminApi.deleteConstructionResourceRequest(projectId, row.id, request)
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
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <form onSubmit={save} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Boxes className="h-5 w-5" />
                {t(editing ? "common.edit" : "construction.resource.new")}
              </DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 sm:grid-cols-2">
              <FieldScope className="space-y-1.5">
                <Label>{t("construction.resource.type")}</Label>
                <Select
                  value={form.type}
                  onValueChange={(value) =>
                    setForm({ ...form, type: value as ConstructionResourceType })
                  }
                >
                  <SelectTrigger data-testid="resource-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {t(`construction.resource.type.${type}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FieldScope>
              <ProjectWbsField
                projectId={projectId}
                items={wbsItems}
                value={form.projectScheduleNodeId}
                onChange={(value) => {
                  const selected = wbsItems.find((item) => item.nodeId === value);
                  setForm({
                    ...form,
                    projectScheduleNodeId: value,
                    wbsCode: selected?.wbsCode ?? form.wbsCode,
                  });
                }}
                loading={wbsLoading}
                failed={wbsFailed}
                onRetry={retryWbs}
                legacyWbsCode={editing?.projectScheduleNodeId ? null : editing?.wbsCode}
                disabled={saving}
              />
              <div className="space-y-1.5">
                <Label htmlFor="resource-date">{t("construction.resource.needBy")}</Label>
                <Input
                  id="resource-date"
                  type="date"
                  required
                  value={form.needByDate}
                  onChange={(e) => setForm({ ...form, needByDate: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-quantity">{t("construction.resource.quantity")}</Label>
                <Input
                  id="resource-quantity"
                  type="number"
                  required
                  min="0.000001"
                  step="any"
                  value={form.quantity}
                  onChange={(e) => setForm({ ...form, quantity: e.target.value })}
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-unit">{t("construction.resource.unit")}</Label>
                <Input
                  id="resource-unit"
                  required
                  maxLength={50}
                  value={form.unit}
                  onChange={(e) => setForm({ ...form, unit: e.target.value })}
                />
              </div>
              {form.type === "Material" && (
                <>
                  <div className="space-y-1.5">
                    <Label htmlFor="resource-boq">{t("construction.resource.boqLine")}</Label>
                    <Select
                      required
                      value={form.projectBoqLineId}
                      onValueChange={(value) => setForm({ ...form, projectBoqLineId: value })}
                    >
                      <SelectTrigger id="resource-boq" data-testid="resource-boq-line">
                        <SelectValue placeholder={t("construction.resource.boqLine")} />
                      </SelectTrigger>
                      <SelectContent>
                        {boqLines.map((line) => (
                          <SelectItem key={line.id} value={String(line.id)}>
                            {line.itemCode} · {line.description}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <UserSelect
                    id="resource-site-user"
                    label={t("procurement.field.responsibleSite")}
                    value={form.responsibleSiteUserId}
                    users={siteUsers}
                    onChange={(value) => setForm({ ...form, responsibleSiteUserId: value })}
                  />
                  <UserSelect
                    id="resource-procurement-user"
                    label={t("procurement.field.procurementOwner")}
                    value={form.assignedProcurementUserId}
                    users={procurementUsers}
                    onChange={(value) => setForm({ ...form, assignedProcurementUserId: value })}
                  />
                </>
              )}
              <div className="space-y-1.5 sm:col-span-2">
                <Label htmlFor="resource-reason">{t("construction.resource.reason")}</Label>
                <Textarea
                  id="resource-reason"
                  required
                  maxLength={2000}
                  value={form.reason}
                  onChange={(e) => setForm({ ...form, reason: e.target.value })}
                />
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={saving} data-testid="resource-save">
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}

function UserSelect({
  id,
  label,
  value,
  users,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  users: UserOption[];
  onChange: (value: string) => void;
}) {
  const { t } = useI18n();
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <SearchableSelect
        required
        id={id}
        data-testid={id}
        ariaLabel={label}
        value={value}
        onChange={onChange}
        placeholder={label}
        options={users.map((user) => personOption(user, t))}
      />
    </div>
  );
}
