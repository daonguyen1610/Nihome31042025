import { useAppSelector } from "@/store";
import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { useListPage } from "@/hooks/useListPage";
import { ListPagination } from "@/components/admin/ListPagination";
import { FieldScope } from "@/components/ui/field-scope";
import { cloneElement, isValidElement, useEffect, useId, useState } from "react";
import { personOption, type PersonIdentity } from "@/lib/people";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  Check,
  ClipboardCheck,
  FilePenLine,
  MapPin,
  Plus,
  Search,
  ShieldAlert,
  Wrench,
  X,
} from "lucide-react";
import { type ScopeProps } from "./constructionScope";
import { ConstructionDeletionControl } from "./ConstructionDeletionControl";
import { PageEmpty, PageError, PageLoading } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { HSE_SEVERITY_TONE, HSE_STATUS_TONE, statusToneClass } from "./constructionStatus";
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
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type CorrectHseViolationRequest,
  type HseViolationFieldsRequest,
  type HseViolationResponse,
  type HseViolationSeverity,
  type HseViolationStatus,
  type OperationalProjectResponse,
  type UpdateHseViolationRequest,
} from "@/services/adminApi";
import { dateTimeLocalToIso, toDateTimeLocalValue, formatDateTime } from "@/lib/displaySettings";

const STATUSES: HseViolationStatus[] = [
  "Draft",
  "Reported",
  "Confirmed",
  "Remediated",
  "Closed",
  "Rejected",
  "Cancelled",
];
const SEVERITIES: HseViolationSeverity[] = ["Low", "Medium", "High", "Critical"];

interface UserOption extends PersonIdentity {
  id: number;
  name: string;
}

interface HseForm {
  occurredAt: string;
  location: string;
  category: string;
  severity: HseViolationSeverity;
  description: string;
  regulatoryReference: string;
  responsibleSiteUserId: string;
  remediationOwnerUserId: string;
  remediationDeadline: string;
  remediationNote: string;
  penaltyReference: string;
  penaltyAmount: string;
  reason: string;
}

type FormMode = "create" | "edit" | "correct";
type DecisionAction = "reject" | "cancel" | "remediate" | null;

const toLocalInputValue = (value: string) => toDateTimeLocalValue(value);

const emptyForm = (): HseForm => ({
  occurredAt: toDateTimeLocalValue(new Date()),
  location: "",
  category: "",
  severity: "Low",
  description: "",
  regulatoryReference: "",
  responsibleSiteUserId: "",
  remediationOwnerUserId: "",
  remediationDeadline: "",
  remediationNote: "",
  penaltyReference: "",
  penaltyAmount: "",
  reason: "",
});

const fromRecord = (row: HseViolationResponse): HseForm => ({
  occurredAt: toLocalInputValue(row.occurredAt),
  location: row.location,
  category: row.category,
  severity: row.severity,
  description: row.description,
  regulatoryReference: row.regulatoryReference ?? "",
  responsibleSiteUserId: String(row.responsibleSiteUserId),
  remediationOwnerUserId: row.remediationOwnerUserId ? String(row.remediationOwnerUserId) : "",
  remediationDeadline: row.remediationDeadline ? toLocalInputValue(row.remediationDeadline) : "",
  remediationNote: row.remediationNote ?? "",
  penaltyReference: row.penaltyReference ?? "",
  penaltyAmount: row.penaltyAmount == null ? "" : String(row.penaltyAmount),
  reason: "",
});

const dateTime = (value: string | null | undefined, lang: string) =>
  value ? formatDateTime(value, lang) : "-";

export const HseViolationsPanel = ({ projectId, scope = "execute" }: ScopeProps) => {
  const uiError = useUiError();
  const [searchParams] = useSearchParams();
  const sourceViolationId = Number(searchParams.get("violationId"));
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionHseManage);
  const canDelete = has(ADMIN_PERMS.constructionHseDelete);
  const canConfirm = scope === "review" && has(ADMIN_PERMS.constructionHseConfirm);
  const currentUserId = useAppSelector((state) => state.auth.user?.userId);
  const canClose = scope === "review" && has(ADMIN_PERMS.constructionHseClose);

  const [project, setProject] = useState<OperationalProjectResponse | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [rows, setRows] = useState<HseViolationResponse[]>([]);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<HseViolationStatus, number>>>({});
  const [status, setStatus] = useState<HseViolationStatus | "all">("all");
  const [search, setSearch] = useState("");
  const deferredSearch = useDebouncedValue(search.trim());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [detail, setDetail] = useState<HseViolationResponse | null>(null);
  useEffect(() => {
    setDetail(null);
    const sourceId = sourceViolationId;
    if (!Number.isInteger(sourceId) || sourceId <= 0) return;
    let active = true;
    void adminApi
      .getHseViolation(projectId, sourceId)
      .then(({ data }) => {
        if (active) setDetail(data);
      })
      .catch((cause) => {
        if (active) setError(uiError(cause));
      });
    return () => {
      active = false;
    };
  }, [projectId, sourceViolationId, uiError]);

  const [formOpen, setFormOpen] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>("create");
  const [formRecord, setFormRecord] = useState<HseViolationResponse | null>(null);
  const [form, setForm] = useState<HseForm>(() => emptyForm());
  const [saving, setSaving] = useState(false);

  const [decisionAction, setDecisionAction] = useState<DecisionAction>(null);
  const [decisionRecord, setDecisionRecord] = useState<HseViolationResponse | null>(null);
  const [decisionReason, setDecisionReason] = useState("");
  const [remediationOwnerId, setRemediationOwnerId] = useState("");
  const [remediationNote, setRemediationNote] = useState("");
  const [acting, setActing] = useState(false);

  useEffect(() => {
    let active = true;
    void adminApi
      .getOperationalProject(projectId)
      .then(({ data }) => {
        if (active) setProject(data);
      })
      .catch((requestError) => {
        if (active) setError(uiError(requestError));
      });
    return () => {
      active = false;
    };
  }, [projectId, uiError]);

  const [page, setPage] = useListPage(JSON.stringify([projectId, deferredSearch, status]));
  const [total, setTotal] = useState(0);
  useEffect(() => {
    if (!projectId) {
      setRows([]);
      setLoading(false);
      return;
    }
    let active = true;
    setLoading(true);
    setError(null);
    void Promise.all([
      adminApi.listHseViolations(projectId, {
        status: status === "all" ? undefined : status,
        search: deferredSearch || undefined,
        page,
        pageSize: 50,
      }),
      adminApi.getOperationalProjectTeam(projectId),
    ])
      .then(([listResponse, teamResponse]) => {
        if (!active) return;
        setRows(listResponse.data.items ?? []);
        setTotal(listResponse.data.total);
        setStatusCounts(listResponse.data.statusCounts ?? {});
        const options = new Map<number, UserOption>();
        if (project?.projectManagerUserId) {
          options.set(project.projectManagerUserId, {
            id: project.projectManagerUserId,
            name: project.projectManagerName ?? `#${project.projectManagerUserId}`,
            roleLabelKey: "designProjects.team.role.ProjectManager",
          });
        }
        for (const member of teamResponse.data.members.filter((item) => item.isActive)) {
          options.set(member.userId, { ...member, id: member.userId, name: member.userName });
        }
        setUsers(Array.from(options.values()));
      })
      .catch((requestError) => {
        if (active) setError(uiError(requestError));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [deferredSearch, projectId, project, status, page, reloadToken, uiError]);

  const refresh = async (focusId?: number) => {
    if (!projectId) return;
    const { data } = await adminApi.listHseViolations(projectId, {
      status: status === "all" ? undefined : status,
      search: deferredSearch || undefined,
      page,
      pageSize: 50,
    });
    setRows(data.items ?? []);
    setTotal(data.total);
    setStatusCounts(data.statusCounts ?? {});
    if (focusId) {
      const focused = await adminApi.getHseViolation(projectId, focusId);
      setDetail(focused.data);
    }
  };

  const openCreate = () => {
    const next = emptyForm();
    if (project?.projectManagerUserId)
      next.responsibleSiteUserId = String(project.projectManagerUserId);
    setForm(next);
    setFormRecord(null);
    setFormMode("create");
    setFormOpen(true);
  };

  const openEdit = (row: HseViolationResponse, mode: FormMode) => {
    setForm(fromRecord(row));
    setFormRecord(row);
    setFormMode(mode);
    setFormOpen(true);
  };

  const buildFields = (): HseViolationFieldsRequest | null => {
    if (
      !form.occurredAt ||
      !form.location.trim() ||
      !form.category.trim() ||
      !form.description.trim() ||
      !form.responsibleSiteUserId
    )
      return null;
    const occurredAt = dateTimeLocalToIso(form.occurredAt);
    if (!occurredAt) return null;
    return {
      offlineClientId: formRecord?.offlineClientId ?? crypto.randomUUID(),
      occurredAt,
      location: form.location.trim(),
      category: form.category.trim(),
      severity: form.severity,
      description: form.description.trim(),
      regulatoryReference: form.regulatoryReference.trim() || null,
      evidenceDocuments: formRecord?.evidenceDocuments ?? [],
      responsibleSiteUserId: Number(form.responsibleSiteUserId),
      remediationOwnerUserId: form.remediationOwnerUserId
        ? Number(form.remediationOwnerUserId)
        : null,
      remediationDeadline: form.remediationDeadline
        ? dateTimeLocalToIso(form.remediationDeadline)
        : null,
      remediationNote: form.remediationNote.trim() || null,
      penaltyReference: form.penaltyReference.trim() || null,
      penaltyAmount: form.penaltyAmount ? Number(form.penaltyAmount) : null,
    };
  };

  const saveForm = async () => {
    if (!projectId) return;
    const fields = buildFields();
    if (!fields || (formMode === "correct" && form.reason.trim().length < 3)) {
      toast({ title: t("hse.required"), variant: "destructive" });
      return;
    }
    setSaving(true);
    try {
      let saved: HseViolationResponse;
      if (formMode === "create") {
        saved = (await adminApi.createHseViolation(projectId, fields, crypto.randomUUID())).data;
      } else if (formMode === "edit" && formRecord) {
        const body: UpdateHseViolationRequest = { ...fields, rowVersion: formRecord.rowVersion };
        saved = (
          await adminApi.updateHseViolation(projectId, formRecord.id, body, crypto.randomUUID())
        ).data;
      } else if (formRecord) {
        const body: CorrectHseViolationRequest = {
          ...fields,
          rowVersion: formRecord.rowVersion,
          reason: form.reason.trim(),
        };
        saved = (
          await adminApi.correctHseViolation(projectId, formRecord.id, body, crypto.randomUUID())
        ).data;
      } else return;
      setFormOpen(false);
      setDetail(saved);
      await refresh(saved.id);
      toast({ title: t("hse.saved") });
    } catch (requestError) {
      toast({ title: uiError(requestError), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  };

  const transition = async (
    row: HseViolationResponse,
    action: "report" | "confirm" | "reject" | "remediate" | "cancel" | "close",
    extra: { reason?: string; remediationOwnerUserId?: number; remediationNote?: string } = {},
  ) => {
    if (!projectId) return;
    setActing(true);
    try {
      const { data } = await adminApi.transitionHseViolation(
        projectId,
        row.id,
        action,
        { ...extra, rowVersion: row.rowVersion },
        crypto.randomUUID(),
      );
      setDetail(data);
      setDecisionAction(null);
      await refresh(data.id);
      toast({ title: t("hse.transitioned") });
    } catch (requestError) {
      toast({ title: uiError(requestError), variant: "destructive" });
    } finally {
      setActing(false);
    }
  };

  const openDecision = (row: HseViolationResponse, action: DecisionAction) => {
    setDecisionRecord(row);
    setDecisionAction(action);
    setDecisionReason("");
    setRemediationOwnerId(
      row.remediationOwnerUserId
        ? String(row.remediationOwnerUserId)
        : String(row.responsibleSiteUserId),
    );
    setRemediationNote(row.remediationNote ?? "");
  };

  const submitDecision = async () => {
    if (!decisionRecord || !decisionAction) return;
    if (decisionAction === "remediate") {
      if (!remediationOwnerId || !remediationNote.trim()) return;
      await transition(decisionRecord, "remediate", {
        remediationOwnerUserId: Number(remediationOwnerId),
        remediationNote: remediationNote.trim(),
      });
      return;
    }
    if (!decisionReason.trim()) return;
    await transition(decisionRecord, decisionAction, { reason: decisionReason.trim() });
  };

  const isOwnReport = (row: HseViolationResponse) =>
    row.reportedByUserId === currentUserId ||
    row.events.some((event) => event.type === "Created" && event.createdByUserId === currentUserId);

  const actionButtons = (row: HseViolationResponse) => (
    <div className="flex flex-wrap gap-2">
      {canManage && row.status === "Draft" && (
        <>
          <Button size="sm" variant="outline" onClick={() => openEdit(row, "edit")}>
            <FilePenLine className="mr-2 h-4 w-4" />
            {t("hse.edit")}
          </Button>
          <Button size="sm" onClick={() => void transition(row, "report")}>
            <ShieldAlert className="mr-2 h-4 w-4" />
            {t("hse.action.report")}
          </Button>
        </>
      )}
      {canConfirm && row.status === "Reported" && (
        <>
          <Button
            size="sm"
            disabled={isOwnReport(row)}
            onClick={() => void transition(row, "confirm")}
          >
            <Check className="mr-2 h-4 w-4" />
            {t("hse.action.confirm")}
          </Button>
          <Button
            size="sm"
            variant="destructive"
            disabled={isOwnReport(row)}
            onClick={() => openDecision(row, "reject")}
          >
            <X className="mr-2 h-4 w-4" />
            {t("hse.action.reject")}
          </Button>
          {isOwnReport(row) && (
            <p className="w-full text-sm text-muted-foreground">
              {t("hse.independentReviewRequired")}
            </p>
          )}
        </>
      )}
      {canManage && row.status === "Confirmed" && (
        <Button size="sm" onClick={() => openDecision(row, "remediate")}>
          <Wrench className="mr-2 h-4 w-4" />
          {t("hse.action.remediate")}
        </Button>
      )}
      {canConfirm && row.status === "Confirmed" && (
        <Button size="sm" variant="outline" onClick={() => openDecision(row, "cancel")}>
          <X className="mr-2 h-4 w-4" />
          {t("hse.action.cancel")}
        </Button>
      )}
      {canClose && row.status === "Remediated" && (
        <Button size="sm" onClick={() => void transition(row, "close")}>
          <ClipboardCheck className="mr-2 h-4 w-4" />
          {t("hse.action.close")}
        </Button>
      )}
      {canConfirm && ["Confirmed", "Remediated", "Closed"].includes(row.status) && (
        <Button size="sm" variant="outline" onClick={() => openEdit(row, "correct")}>
          <FilePenLine className="mr-2 h-4 w-4" />
          {t("hse.correct")}
        </Button>
      )}
      {canDelete && ["Draft", "Rejected", "Cancelled"].includes(row.status) && (
        <ConstructionDeletionControl
          rowVersion={row.rowVersion}
          getImpact={() => adminApi.getHseViolationDeletionImpact(projectId, row.id)}
          deleteRecord={(request) => adminApi.deleteHseViolation(projectId, row.id, request)}
          onDeleted={() => {
            if (detail?.id === row.id) setDetail(null);
            setReloadToken((value) => value + 1);
          }}
        />
      )}
    </div>
  );

  return (
    <>
      <div className="space-y-4">
        <div className="flex justify-end">
          {canManage && (
            <Button onClick={openCreate}>
              <Plus className="mr-2 h-4 w-4" />
              {t("hse.new")}
            </Button>
          )}
        </div>

        <section className="space-y-5 rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="space-y-1.5">
            <Label htmlFor="hse-search">{t("hse.search")}</Label>
            <div className="relative">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                id="hse-search"
                type="search"
                value={search}
                placeholder={t("hse.searchPlaceholder")}
                onChange={(event) => setSearch(event.target.value)}
                className="pl-9"
              />
            </div>
          </div>

          <div className="flex gap-2 overflow-x-auto pb-1">
            <button
              type="button"
              onClick={() => setStatus("all")}
              aria-pressed={status === "all"}
              className={cn(
                "status-pill shrink-0 border border-border bg-card text-foreground",
                status === "all" && "shadow-[inset_0_0_0_2px_currentColor]",
              )}
            >
              {t("hse.allStatuses")}
            </button>
            {STATUSES.map((item) => (
              <button
                key={item}
                type="button"
                onClick={() => setStatus(item)}
                aria-pressed={status === item}
                className={cn(
                  "shrink-0",
                  statusToneClass(HSE_STATUS_TONE[item]),
                  status === item && "shadow-[inset_0_0_0_2px_currentColor]",
                )}
              >
                {t(`hse.status.${item}`)}{" "}
                <span className="ml-1 tabular-nums">{statusCounts[item] ?? 0}</span>
              </button>
            ))}
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => setReloadToken((value) => value + 1)} />
        ) : rows.length === 0 ? (
          <PageEmpty
            message={t("hse.empty")}
            action={
              canManage && (
                <Button size="sm" onClick={openCreate}>
                  <Plus className="mr-2 h-4 w-4" />
                  {t("hse.new")}
                </Button>
              )
            }
          />
        ) : (
          <>
            <div className="space-y-3 md:hidden">
              {rows.map((row) => (
                <button
                  key={row.id}
                  type="button"
                  onClick={() => setDetail(row)}
                  className="w-full rounded-xl border border-border bg-card p-4 text-left shadow-sm"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-bold text-foreground">{row.code}</p>
                      <p className="mt-1 line-clamp-2 text-sm text-foreground">{row.description}</p>
                    </div>
                    <ConstructionStatusBadge tone={HSE_STATUS_TONE[row.status]}>
                      {t(`hse.status.${row.status}`)}
                    </ConstructionStatusBadge>
                  </div>
                  <div className="mt-4 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                    <ConstructionStatusBadge tone={HSE_SEVERITY_TONE[row.severity]}>
                      {t(`hse.severity.${row.severity}`)}
                    </ConstructionStatusBadge>
                    <span className="flex items-center gap-1">
                      <MapPin className="h-3.5 w-3.5" />
                      {row.location}
                    </span>
                    <span>{dateTime(row.occurredAt, lang)}</span>
                  </div>
                </button>
              ))}
            </div>
            <p className="hidden text-xs text-muted-foreground md:block lg:hidden">
              {t("construction.cm.scrollTableHint")}
            </p>
            <div className="hidden overflow-x-auto rounded-xl border border-border bg-card shadow-sm md:block">
              <table className="w-full min-w-[900px] text-left text-sm">
                <thead className="bg-muted text-xs font-semibold uppercase text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3">{t("hse.field.description")}</th>
                    <th className="min-w-[140px] px-4 py-3">{t("hse.field.location")}</th>
                    <th className="min-w-[160px] px-4 py-3">{t("hse.field.responsible")}</th>
                    <th className="w-[110px] px-4 py-3">{t("hse.field.severity")}</th>
                    <th className="w-[110px] px-4 py-3">{t("hse.field.status")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {rows.map((row) => (
                    <tr
                      key={row.id}
                      onClick={() => setDetail(row)}
                      className="cursor-pointer bg-card hover:bg-muted/50"
                    >
                      <td className="max-w-md px-4 py-3">
                        <p className="font-semibold text-foreground">{row.code}</p>
                        <p className="truncate text-muted-foreground">{row.description}</p>
                      </td>
                      <td className="px-4 py-3 text-foreground">{row.location}</td>
                      <td className="px-4 py-3 text-foreground">
                        {row.responsibleSiteUserName ?? `#${row.responsibleSiteUserId}`}
                      </td>
                      <td className="whitespace-nowrap px-4 py-3">
                        <ConstructionStatusBadge tone={HSE_SEVERITY_TONE[row.severity]}>
                          {t(`hse.severity.${row.severity}`)}
                        </ConstructionStatusBadge>
                      </td>
                      <td className="whitespace-nowrap px-4 py-3">
                        <ConstructionStatusBadge tone={HSE_STATUS_TONE[row.status]}>
                          {t(`hse.status.${row.status}`)}
                        </ConstructionStatusBadge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
        <ListPagination
          page={page}
          pageSize={50}
          total={total}
          onPageChange={setPage}
          disabled={loading}
        />
      </div>

      <Sheet
        open={Boolean(detail)}
        onOpenChange={(open) => {
          if (!open) setDetail(null);
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
          {detail && (
            <div className="space-y-6">
              <SheetHeader>
                <SheetTitle className="flex items-center justify-between gap-3 pr-7">
                  <span>{detail.code}</span>
                  <ConstructionStatusBadge tone={HSE_STATUS_TONE[detail.status]}>
                    {t(`hse.status.${detail.status}`)}
                  </ConstructionStatusBadge>
                </SheetTitle>
              </SheetHeader>
              <div className="rounded-lg bg-muted/40 p-4">
                <p className="text-sm font-semibold text-foreground">{detail.description}</p>
                <p className="mt-2 text-sm text-muted-foreground">
                  {detail.category} - {detail.location}
                </p>
              </div>
              <dl className="grid grid-cols-2 gap-x-4 gap-y-4 text-sm">
                <div>
                  <dt className="text-muted-foreground">{t("hse.field.occurredAt")}</dt>
                  <dd className="mt-1 font-medium">{dateTime(detail.occurredAt, lang)}</dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t("hse.field.severity")}</dt>
                  <dd className="mt-1">
                    <ConstructionStatusBadge tone={HSE_SEVERITY_TONE[detail.severity]}>
                      {t(`hse.severity.${detail.severity}`)}
                    </ConstructionStatusBadge>
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t("hse.field.responsible")}</dt>
                  <dd className="mt-1 font-medium">
                    {detail.responsibleSiteUserName ?? `#${detail.responsibleSiteUserId}`}
                  </dd>
                </div>
                <div>
                  <dt className="text-muted-foreground">{t("hse.field.remediationOwner")}</dt>
                  <dd className="mt-1 font-medium">{detail.remediationOwnerUserName ?? "-"}</dd>
                </div>
              </dl>
              {detail.remediationNote && (
                <div className="border border-border bg-muted/50 p-3 text-sm text-foreground">
                  <p className="font-semibold">{t("hse.field.remediationNote")}</p>
                  <p className="mt-1">{detail.remediationNote}</p>
                </div>
              )}
              {actionButtons(detail)}
              <section>
                <h2 className="mb-3 text-sm font-bold text-foreground">{t("hse.history")}</h2>
                <ol className="space-y-3 border-l border-border pl-4">
                  {detail.events.map((event) => (
                    <li key={event.id} className="text-sm">
                      <p className="font-semibold text-foreground">
                        {event.type === "Correction"
                          ? t("hse.correct")
                          : t(`hse.status.${event.toStatus}`)}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {dateTime(event.createdAt, lang)} -{" "}
                        {event.createdByName ?? `#${event.createdByUserId}`}
                      </p>
                      {event.reason && <p className="mt-1 text-foreground">{event.reason}</p>}
                    </li>
                  ))}
                </ol>
              </section>
            </div>
          )}
        </SheetContent>
      </Sheet>

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-h-[92vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {formMode === "create"
                ? t("hse.new")
                : formMode === "edit"
                  ? t("hse.edit")
                  : t("hse.correct")}
            </DialogTitle>
          </DialogHeader>
          {formMode !== "correct" && (
            <p className="text-sm text-muted-foreground">{t("hse.formIntro")}</p>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t("hse.field.occurredAt")} hint={t("hse.hint.occurredAt")} required>
              <Input
                type="datetime-local"
                value={form.occurredAt}
                onChange={(event) => setForm({ ...form, occurredAt: event.target.value })}
              />
            </Field>
            <Field label={t("hse.field.location")} hint={t("hse.hint.location")} required>
              <Input
                value={form.location}
                maxLength={300}
                placeholder={t("hse.placeholder.location")}
                onChange={(event) => setForm({ ...form, location: event.target.value })}
              />
            </Field>
            <Field label={t("hse.field.category")} hint={t("hse.hint.category")} required>
              <Input
                value={form.category}
                maxLength={100}
                placeholder={t("hse.placeholder.category")}
                onChange={(event) => setForm({ ...form, category: event.target.value })}
              />
            </Field>
            <Field label={t("hse.field.severity")}>
              <Select
                value={form.severity}
                onValueChange={(value) =>
                  setForm({ ...form, severity: value as HseViolationSeverity })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {SEVERITIES.map((item) => (
                    <SelectItem key={item} value={item}>
                      {t(`hse.severity.${item}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <div className="sm:col-span-2">
              <Field label={t("hse.field.description")} hint={t("hse.hint.description")} required>
                <Textarea
                  value={form.description}
                  maxLength={4000}
                  rows={4}
                  placeholder={t("hse.placeholder.description")}
                  onChange={(event) => setForm({ ...form, description: event.target.value })}
                />
              </Field>
            </div>
            <Field label={t("hse.field.responsible")} hint={t("hse.hint.responsible")} required>
              <UserSelect
                value={form.responsibleSiteUserId}
                users={users}
                onChange={(value) => setForm({ ...form, responsibleSiteUserId: value })}
              />
            </Field>
            <Field
              label={t("hse.field.regulatoryReference")}
              hint={t("hse.hint.regulatoryReference")}
            >
              <Input
                value={form.regulatoryReference}
                maxLength={500}
                placeholder={t("hse.placeholder.regulatoryReference")}
                onChange={(event) => setForm({ ...form, regulatoryReference: event.target.value })}
              />
            </Field>
            <Field label={t("hse.field.remediationOwner")} hint={t("hse.hint.remediationOwner")}>
              <UserSelect
                value={form.remediationOwnerUserId}
                users={users}
                optional
                onChange={(value) => setForm({ ...form, remediationOwnerUserId: value })}
              />
            </Field>
            <Field
              label={t("hse.field.remediationDeadline")}
              hint={t("hse.hint.remediationDeadline")}
            >
              <Input
                type="datetime-local"
                value={form.remediationDeadline}
                onChange={(event) => setForm({ ...form, remediationDeadline: event.target.value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Field label={t("hse.field.remediationNote")} hint={t("hse.hint.remediationNote")}>
                <Textarea
                  value={form.remediationNote}
                  maxLength={4000}
                  onChange={(event) => setForm({ ...form, remediationNote: event.target.value })}
                />
              </Field>
            </div>
            {canConfirm && (
              <>
                <Field
                  label={t("hse.field.penaltyReference")}
                  hint={t("hse.hint.penaltyReference")}
                >
                  <Input
                    value={form.penaltyReference}
                    maxLength={200}
                    onChange={(event) => setForm({ ...form, penaltyReference: event.target.value })}
                  />
                </Field>
                <Field label={t("hse.field.penaltyAmount")} hint={t("hse.hint.penaltyAmount")}>
                  <Input
                    type="number"
                    min="0"
                    step="0.01"
                    value={form.penaltyAmount}
                    onChange={(event) => setForm({ ...form, penaltyAmount: event.target.value })}
                  />
                </Field>
              </>
            )}
            {formMode === "correct" && (
              <div className="sm:col-span-2">
                <Field label={t("hse.field.reason")} hint={t("hse.hint.reason")} required>
                  <Textarea
                    value={form.reason}
                    minLength={3}
                    maxLength={2000}
                    onChange={(event) => setForm({ ...form, reason: event.target.value })}
                  />
                </Field>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              {t("hse.cancel")}
            </Button>
            <Button disabled={saving} onClick={() => void saveForm()}>
              {t("hse.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(decisionAction)}
        onOpenChange={(open) => {
          if (!open) setDecisionAction(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{decisionAction ? t(`hse.action.${decisionAction}`) : ""}</DialogTitle>
          </DialogHeader>
          {decisionAction === "remediate" ? (
            <div className="space-y-4">
              <Field label={t("hse.field.remediationOwner")} hint={t("hse.hint.remediationOwner")}>
                <UserSelect
                  value={remediationOwnerId}
                  users={users}
                  onChange={setRemediationOwnerId}
                />
              </Field>
              <Field label={t("hse.field.remediationNote")} hint={t("hse.hint.remediationNote")}>
                <Textarea
                  value={remediationNote}
                  maxLength={4000}
                  onChange={(event) => setRemediationNote(event.target.value)}
                />
              </Field>
            </div>
          ) : (
            <Field label={t("hse.field.reason")} hint={t("hse.hint.reason")} required>
              <Textarea
                value={decisionReason}
                maxLength={2000}
                onChange={(event) => setDecisionReason(event.target.value)}
              />
            </Field>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDecisionAction(null)}>
              {t("hse.cancel")}
            </Button>
            <Button disabled={acting} onClick={() => void submitDecision()}>
              {t("hse.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
};

const Field = ({
  label,
  hint,
  required = false,
  children,
}: {
  label: string;
  hint?: string;
  required?: boolean;
  children: React.ReactNode;
}) => {
  const hintId = useId();
  return (
    <FieldScope className="space-y-1.5">
      <Label>
        {label}
        {required && (
          <span className="ml-0.5 text-destructive" aria-hidden="true">
            *
          </span>
        )}
      </Label>
      {isValidElement(children)
        ? cloneElement(children as React.ReactElement<Record<string, unknown>>, {
            "aria-describedby": hint ? hintId : undefined,
            "aria-required": required || undefined,
          })
        : children}
      {hint && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
    </FieldScope>
  );
};

const UserSelect = ({
  value,
  users,
  optional = false,
  onChange,
}: {
  value: string;
  users: UserOption[];
  optional?: boolean;
  onChange: (value: string) => void;
}) => {
  const { t } = useI18n();
  return (
    <SearchableSelect
      ariaLabel={t("people.select")}
      value={value || (optional ? "none" : "")}
      onChange={(next) => onChange(next === "none" ? "" : next)}
      options={[
        ...(optional ? [{ value: "none", label: t("designProjects.team.value.none") }] : []),
        ...users.map((user) => ({ ...personOption(user, t), value: String(user.id) })),
      ]}
    />
  );
};
