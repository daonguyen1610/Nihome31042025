import { FormEvent, useCallback, useEffect, useState } from "react";
import { Boxes, Plus, Send } from "lucide-react";
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
  type ConstructionResourceRequestResponse,
  type ConstructionResourceType,
  type ConstructionResourceReferenceDataResponse,
} from "@/services/adminApi";

const TYPES: ConstructionResourceType[] = ["Material", "Equipment", "Labour"];
type UserOption = { userId: number; userName: string };
type BoqLineOption = ConstructionResourceReferenceDataResponse["boqLines"][number];

export default function BchResourceRequestsPage() {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [projectId, setProjectId] = useState<number | null>(null);
  const selectProject = useCallback((id: number) => setProjectId(id), []);
  const [rows, setRows] = useState<ConstructionResourceRequestResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [siteUsers, setSiteUsers] = useState<UserOption[]>([]);
  const [procurementUsers, setProcurementUsers] = useState<UserOption[]>([]);
  const [boqLines, setBoqLines] = useState<BoqLineOption[]>([]);
  const [form, setForm] = useState({
    type: "Material" as ConstructionResourceType,
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
    if (projectId == null) return;
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
      .catch(
        (cause) => active && setError(extractApiError(cause, t("construction.resource.loadError"))),
      )
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [projectId, reload, t]);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    if (projectId == null) return;
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
    setSaving(true);
    try {
      await adminApi.createConstructionResourceRequest(projectId, {
        type: form.type,
        wbsCode: form.wbsCode.trim(),
        needByDate: form.needByDate,
        quantity: Number(form.quantity),
        unit: form.unit.trim(),
        reason: form.reason.trim(),
        projectBoqLineId: form.projectBoqLineId ? Number(form.projectBoqLineId) : null,
        responsibleSiteUserId: form.responsibleSiteUserId
          ? Number(form.responsibleSiteUserId)
          : null,
        assignedProcurementUserId: form.assignedProcurementUserId
          ? Number(form.assignedProcurementUserId)
          : null,
      });
      setOpen(false);
      setForm({
        type: "Material",
        wbsCode: "",
        needByDate: "",
        quantity: "",
        unit: "",
        reason: "",
        projectBoqLineId: "",
        responsibleSiteUserId: "",
        assignedProcurementUserId: "",
      });
      setReload((value) => value + 1);
      toast({ title: t("construction.resource.created") });
    } catch (cause) {
      toast({
        variant: "destructive",
        title: extractApiError(cause, t("construction.resource.saveError")),
      });
    } finally {
      setSaving(false);
    }
  };

  const submit = async (row: ConstructionResourceRequestResponse) => {
    if (projectId == null) return;
    try {
      await adminApi.submitConstructionResourceRequest(projectId, row.id, row.rowVersion);
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
      title={t("construction.resource.title")}
      description={t("construction.resource.description")}
      projectId={projectId}
      onProjectChange={selectProject}
      testId="bch-resource-page"
    >
      <section className="space-y-4">
        <div className="flex justify-end">
          {has(ADMIN_PERMS.constructionResourceRequestsCreate) && (
            <Button onClick={() => setOpen(true)} data-testid="resource-new">
              <Plus className="mr-2 h-4 w-4" />
              {t("construction.resource.new")}
            </Button>
          )}
        </div>
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReload((value) => value + 1)} />
        ) : rows.length === 0 ? (
          <PageEmpty message={t("construction.resource.empty")} />
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
                  <Badge variant="outline">{t(`construction.status.${row.status}`)}</Badge>
                </div>
                <dl className="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <dt className="text-muted-foreground">{t("construction.common.wbs")}</dt>
                    <dd>{row.wbsCode}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">{t("construction.resource.needBy")}</dt>
                    <dd>{new Date(row.needByDate).toLocaleDateString(lang)}</dd>
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
                {row.status === "Draft" && (
                  <Button
                    size="sm"
                    className="w-full sm:w-auto"
                    onClick={() => void submit(row)}
                    data-testid={`resource-submit-${row.id}`}
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
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
          <form onSubmit={save} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Boxes className="h-5 w-5" />
                {t("construction.resource.new")}
              </DialogTitle>
            </DialogHeader>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
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
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="resource-wbs">{t("construction.common.wbs")}</Label>
                <Input
                  id="resource-wbs"
                  required
                  maxLength={100}
                  value={form.wbsCode}
                  onChange={(e) => setForm({ ...form, wbsCode: e.target.value })}
                />
              </div>
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
    </ConstructionWorkspaceShell>
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
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Select required value={value} onValueChange={onChange}>
        <SelectTrigger id={id} data-testid={id}>
          <SelectValue placeholder={label} />
        </SelectTrigger>
        <SelectContent>
          {users.map((user) => (
            <SelectItem key={user.userId} value={String(user.userId)}>
              {user.userName}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
