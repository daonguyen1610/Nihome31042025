import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, BriefcaseBusiness, CalendarClock, ExternalLink, FileText, Pencil, Plus, RefreshCcw, Search, ShoppingCart, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import ProjectDocumentsPanel from "@/pages/admin/ProjectDocumentsPanel";
import { PageError, PageLoading } from "@/components/PageState";
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from "@/components/ui/accordion";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { usePermissions } from "@/hooks/usePermissions";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  OPERATIONAL_PROJECT_STATUSES,
  type CustomerResponse,
  type DeletionImpactResponse,
  type OperationalProjectListItemResponse,
  type OperationalProjectResponse,
  type OperationalProjectStatus,
  type OperationalProjectTimelineItem,
  type PaymentMilestoneStatus,
  type UpdateOperationalProjectRequest,
  type UserListItemResponse,
} from "@/services/adminApi";

const statusClass: Record<OperationalProjectStatus, string> = {
  Planning: "border-sky-200 bg-sky-50 text-sky-700",
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  OnHold: "border-amber-200 bg-amber-50 text-amber-700",
  Completed: "border-slate-200 bg-slate-50 text-slate-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const milestoneStatusClass: Record<PaymentMilestoneStatus, string> = {
  Pending: "border-amber-200 bg-amber-50 text-amber-700",
  Requested: "border-sky-200 bg-sky-50 text-sky-700",
  Paid: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

const emptyForm = (): UpdateOperationalProjectRequest => ({
  name: "",
  customerId: 0,
  projectManagerUserId: null,
  startDate: null,
  endDate: null,
  note: "",
  status: "Planning",
});

const dateInput = (value?: string | null) => value?.slice(0, 10) ?? "";
const apiDate = (value: string) => value ? `${value}T00:00:00.000Z` : null;

const OperationalProjects = () => {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.operationalProjectsManage);
  const canViewContracts = has(ADMIN_PERMS.contracts);
  const canListUsers = has(ADMIN_PERMS.users);
  const projectId = id && /^\d+$/.test(id) ? Number(id) : null;
  const customerFilter = /^\d+$/.test(searchParams.get("customerId") ?? "")
    ? Number(searchParams.get("customerId"))
    : undefined;

  const [rows, setRows] = useState<OperationalProjectListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [detail, setDetail] = useState<OperationalProjectResponse | null>(null);
  const [timeline, setTimeline] = useState<OperationalProjectTimelineItem[]>([]);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [users, setUsers] = useState<UserListItemResponse[]>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<OperationalProjectStatus | "">("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<UpdateOperationalProjectRequest>(emptyForm());
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteImpactLoading, setDeleteImpactLoading] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void Promise.all([
      adminApi.listCustomers({ pageSize: 200 }),
      canListUsers
        ? adminApi.getUsers({ take: 200 })
        : Promise.resolve({ data: { items: [], total: 0 } }),
    ]).then(([customerResponse, userResponse]) => {
      if (cancelled) return;
      setCustomers(customerResponse.data.items ?? []);
      setUsers(userResponse.data.items ?? []);
    }).catch(() => {
      // Lookup access is secondary; the main request reports its own error.
    });
    return () => { cancelled = true; };
  }, [canListUsers]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      if (projectId != null) {
        const [detailResponse, timelineResponse] = await Promise.all([
          adminApi.getOperationalProject(projectId),
          adminApi.getOperationalProjectTimeline(projectId),
        ]);
        setDetail(detailResponse.data);
        setTimeline(timelineResponse.data ?? []);
      } else {
        setDetail(null);
        setTimeline([]);
        const response = await adminApi.listOperationalProjects({
          customerId: customerFilter,
          search: search.trim() || undefined,
          status: status || undefined,
          page: 1,
          pageSize: 100,
        });
        setRows(response.data.items ?? []);
        setTotal(response.data.total ?? 0);
      }
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [customerFilter, projectId, search, status]);

  useEffect(() => { void load(); }, [load]);

  const openCreate = () => {
    setForm(emptyForm());
    setFormError(null);
    setDialogOpen(true);
  };

  const openEdit = (project: OperationalProjectResponse) => {
    setForm({
      name: project.name,
      customerId: project.customerId,
      projectManagerUserId: project.projectManagerUserId ?? null,
      startDate: dateInput(project.startDate),
      endDate: dateInput(project.endDate),
      note: project.note ?? "",
      status: project.status,
      rowVersion: project.rowVersion,
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const validate = () => {
    const name = form.name.trim();
    if (!name || name.length > 300) return t("operationalProjects.validation.name");
    if (form.customerId < 1) return t("operationalProjects.validation.customer");
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      return t("operationalProjects.validation.dateRange");
    }
    if ((form.note?.length ?? 0) > 4000) return t("operationalProjects.validation.note");
    return null;
  };

  const save = async () => {
    const validation = validate();
    if (validation) {
      setFormError(validation);
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const payload = {
        ...form,
        name: form.name.trim(),
        note: form.note?.trim() || null,
        startDate: apiDate(dateInput(form.startDate)),
        endDate: apiDate(dateInput(form.endDate)),
      };
      const response = detail
        ? await adminApi.updateOperationalProject(detail.id, payload)
        : await adminApi.createOperationalProject(payload);
      setDialogOpen(false);
      toast({ title: t(detail ? "operationalProjects.updated" : "operationalProjects.created") });
      navigate(`/admin/operational-projects/${response.data.id}`);
    } catch (reason) {
      setFormError(extractApiError(reason));
    } finally {
      setSaving(false);
    }
  };

  const openDelete = async () => {
    if (!detail) return;
    setDeleteOpen(true);
    setDeleteImpact(null);
    setDeleteError(null);
    setDeleteImpactLoading(true);
    try {
      const response = await adminApi.getOperationalProjectDeletionImpact(detail.id);
      setDeleteImpact(response.data);
    } catch (reason) {
      setDeleteError(extractApiError(reason));
    } finally {
      setDeleteImpactLoading(false);
    }
  };

  const remove = async (confirmation: string) => {
    if (!detail || !deleteImpact) return null;
    setDeleting(true);
    setDeleteError(null);
    try {
      const response = await adminApi.deleteOperationalProject(detail.id, {
        planToken: deleteImpact.planToken,
        confirmation,
        rowVersion: detail.rowVersion,
      });
      return response.status === 204 ? null : response.data;
    } catch (reason) {
      setDeleteError(extractApiError(reason));
      throw reason;
    } finally {
      setDeleting(false);
    }
  };

  const completeDelete = () => {
    toast({ title: t("operationalProjects.deleted") });
    setDeleteOpen(false);
    navigate("/admin/operational-projects");
  };

  const dateFormat = useMemo(() => new Intl.DateTimeFormat(lang), [lang]);
  const currencyFormat = useMemo(() => new Intl.NumberFormat(lang, {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }), [lang]);
  const formatDate = (value?: string | null) => value ? dateFormat.format(new Date(value)) : "—";

  return (
    <AdminLayout>
      {loading ? <PageLoading /> : error ? <PageError message={error} onRetry={() => void load()} /> : detail ? (
        <div className="space-y-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <Button variant="ghost" className="mb-2 -ml-3" asChild>
                <Link to="/admin/operational-projects"><ArrowLeft className="mr-2 h-4 w-4" />{t("operationalProjects.back")}</Link>
              </Button>
              <div className="flex flex-wrap items-center gap-2">
                <h1 className="text-2xl font-semibold">{detail.code} · {detail.name}</h1>
                <Badge variant="outline" className={statusClass[detail.status]}>{t(`operationalProjects.status.${detail.status}`)}</Badge>
              </div>
              <p className="mt-1 text-sm text-muted-foreground">{detail.customerName}</p>
            </div>
            {canManage && <div className="flex gap-2">
              <Button variant="outline" onClick={() => openEdit(detail)}><Pencil className="mr-2 h-4 w-4" />{t("common.edit")}</Button>
              <Button variant="destructive" onClick={() => void openDelete()}><Trash2 className="mr-2 h-4 w-4" />{t("common.delete")}</Button>
            </div>}
          </div>

          <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Summary label={t("operationalProjects.field.manager")} value={detail.projectManagerName || "—"} />
            <Summary label={t("operationalProjects.field.period")} value={`${formatDate(detail.startDate)} – ${formatDate(detail.endDate)}`} />
            <Summary label={t("operationalProjects.field.designProject")} value={detail.designProjectCode || "—"} href={detail.designProjectId ? `/admin/design-projects/${detail.designProjectId}` : undefined} />
            <Summary label={t("operationalProjects.field.updatedAt")} value={formatDate(detail.updatedAt)} />
          </section>

          {detail.note && <section className="rounded-lg border bg-card p-4"><h2 className="mb-2 font-medium">{t("operationalProjects.field.note")}</h2><p className="whitespace-pre-wrap text-sm text-muted-foreground">{detail.note}</p></section>}

          <Accordion type="multiple" defaultValue={["documents", "timeline", "opportunities", "quotes", "contracts"]} className="space-y-3">
            <AccordionItem id="project-documents" value="documents" className="scroll-mt-4 rounded-lg border bg-card px-4">
              <AccordionTrigger className="py-4 hover:no-underline" data-testid="project-documents-trigger">
                <div className="flex items-center gap-3">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <span className="font-semibold">{t("operationalProjects.documents.title")}</span>
                </div>
              </AccordionTrigger>
              <AccordionContent className="pb-4">
                <ProjectDocumentsPanel projectId={detail.id} canManage={canManage} />
              </AccordionContent>
            </AccordionItem>

            <AccordionItem value="timeline" className="rounded-lg border bg-card px-4">
              <AccordionTrigger className="py-4 hover:no-underline">
                <div className="flex items-center gap-3">
                  <CalendarClock className="h-4 w-4 text-muted-foreground" />
                  <span className="font-semibold">{t("operationalProjects.timeline.title")}</span>
                  <Badge variant="secondary" className="ml-1">{timeline.length}</Badge>
                </div>
              </AccordionTrigger>
              <AccordionContent className="pb-4">
                <p className="mb-4 text-sm text-muted-foreground">{t("operationalProjects.timeline.description")}</p>
                {timeline.length === 0 ? (
                  <p className="rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">
                    {t("operationalProjects.timeline.empty")}
                  </p>
                ) : (
                  <div className="space-y-3">
                    {timeline.map(item => (
                      <article key={item.id} className="rounded-md border p-4">
                        <div className="flex flex-wrap items-start justify-between gap-3">
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <h3 className="break-words font-medium">{item.name}</h3>
                              <Badge variant="outline" className={milestoneStatusClass[item.status]}>
                                {t(`contracts.milestoneStatus.${item.status}`)}
                              </Badge>
                            </div>
                            {canViewContracts ? (
                              <Link
                                to={`/admin/contracts/${item.contractId}`}
                                className="mt-1 inline-flex items-center gap-1 break-all font-mono text-sm text-primary hover:underline"
                              >
                                {item.contractNumber}<ExternalLink className="h-3.5 w-3.5 shrink-0" />
                              </Link>
                            ) : (
                              <p className="mt-1 break-all font-mono text-sm text-muted-foreground">{item.contractNumber}</p>
                            )}
                          </div>
                          <p className="shrink-0 font-semibold text-primary">{currencyFormat.format(item.amount)}</p>
                        </div>
                        <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
                          <div>
                            <dt className="text-xs text-muted-foreground">{t("operationalProjects.timeline.plannedDate")}</dt>
                            <dd>{formatDate(item.plannedDate)}</dd>
                          </div>
                          <div>
                            <dt className="text-xs text-muted-foreground">{t("operationalProjects.timeline.actualDate")}</dt>
                            <dd>{formatDate(item.actualDate)}</dd>
                          </div>
                          <div>
                            <dt className="text-xs text-muted-foreground">{t("operationalProjects.timeline.updatedAt")}</dt>
                            <dd>{formatDate(item.updatedAt)}</dd>
                          </div>
                          <div>
                            <dt className="text-xs text-muted-foreground">{t("operationalProjects.timeline.source")}</dt>
                            <dd>{t(`operationalProjects.timeline.source.${item.source}`)}</dd>
                          </div>
                        </dl>
                        <p className="mt-3 text-xs text-muted-foreground">
                          {t("operationalProjects.timeline.percent", { percent: item.percentValue })}
                        </p>
                        {item.note && <p className="mt-3 whitespace-pre-wrap break-words border-t pt-3 text-sm text-muted-foreground">{item.note}</p>}
                      </article>
                    ))}
                  </div>
                )}
              </AccordionContent>
            </AccordionItem>

            <AccordionItem value="opportunities" className="rounded-lg border bg-card px-4">
              <AccordionTrigger className="hover:no-underline py-4">
                <div className="flex items-center gap-3">
                  <BriefcaseBusiness className="h-4 w-4 text-muted-foreground" />
                  <span className="font-semibold">{t("operationalProjects.related.opportunities")}</span>
                  <Badge variant="secondary" className="ml-1">{detail.opportunities.length}</Badge>
                </div>
              </AccordionTrigger>
              <AccordionContent className="pb-4">
                {detail.opportunities.length === 0 ? (
                  <p className="py-2 text-sm text-muted-foreground">{t("operationalProjects.related.empty")}</p>
                ) : (
                  <div className="space-y-3">
                    {detail.opportunities.map(item => (
                      <div key={item.id} className="rounded-md border p-4">
                        <div className="flex items-start justify-between gap-3">
                          <div className="min-w-0 flex-1 space-y-2">
                            <div className="flex items-center gap-2">
                              <p className="font-medium">{item.name}</p>
                              <Badge variant="outline" className="text-xs">{t(`opportunities.stage.${item.stage}`)}</Badge>
                            </div>
                            <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                              <div className="flex gap-2">
                                <dt className="text-muted-foreground">{t("opportunities.field.estimatedValue")}:</dt>
                                <dd className="font-medium">{currencyFormat.format(item.estimatedValue)}</dd>
                              </div>
                              <div className="flex gap-2">
                                <dt className="text-muted-foreground">{t("opportunities.field.winProbability")}:</dt>
                                <dd className="font-medium">{item.winProbability}%</dd>
                              </div>
                              {item.expectedCloseDate && (
                                <div className="flex gap-2">
                                  <dt className="text-muted-foreground">{t("opportunities.field.expectedCloseDate")}:</dt>
                                  <dd>{new Date(item.expectedCloseDate).toLocaleDateString()}</dd>
                                </div>
                              )}
                              {item.ownerName && (
                                <div className="flex gap-2">
                                  <dt className="text-muted-foreground">{t("opportunities.field.owner")}:</dt>
                                  <dd>{item.ownerName}</dd>
                                </div>
                              )}
                              {item.lostReasonCode && (
                                <div className="col-span-2 flex gap-2">
                                  <dt className="text-muted-foreground">{t("opportunities.field.lostReason")}:</dt>
                                  <dd>{item.lostReasonCode}</dd>
                                </div>
                              )}
                            </dl>
                          </div>
                          <Button variant="ghost" size="sm" asChild>
                            <Link to={`/admin/opportunities/${item.id}`}><ExternalLink className="h-4 w-4" /></Link>
                          </Button>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </AccordionContent>
            </AccordionItem>

            <AccordionItem value="quotes" className="rounded-lg border bg-card px-4">
              <AccordionTrigger className="hover:no-underline py-4">
                <div className="flex items-center gap-3">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <span className="font-semibold">{t("operationalProjects.related.quotes")}</span>
                  <Badge variant="secondary" className="ml-1">{detail.quotes.length}</Badge>
                </div>
              </AccordionTrigger>
              <AccordionContent className="pb-4">
                {detail.quotes.length === 0 ? (
                  <p className="py-2 text-sm text-muted-foreground">{t("operationalProjects.related.empty")}</p>
                ) : (
                  <div className="space-y-4">
                    {detail.quotes.map(item => (
                      <div key={item.id} className="rounded-md border p-4">
                        <div className="flex items-start justify-between gap-3 mb-3">
                          <div className="flex items-center gap-2 flex-wrap">
                            <p className="font-medium font-mono">{item.code}</p>
                            <Badge variant="outline" className="text-xs">{t(`quotes.status.${item.status}`)}</Badge>
                            {item.isExpired && <Badge variant="destructive" className="text-xs">{t("quotes.expired")}</Badge>}
                            <span className="text-xs text-muted-foreground">v{item.version}</span>
                          </div>
                          <Button variant="ghost" size="sm" asChild>
                            <Link to={`/admin/quotes/${item.id}`}><ExternalLink className="h-4 w-4" /></Link>
                          </Button>
                        </div>
                        
                        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm mb-3">
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("quotes.field.method")}</dt>
                            <dd>{t(`quotes.method.${item.method}`)}</dd>
                          </div>
                          {item.method === "UnitCost" && item.areaSqm && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.area")}</dt>
                              <dd>{item.areaSqm} m²</dd>
                            </div>
                          )}
                          {item.method === "UnitCost" && item.unitPricePerSqm && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.unitPrice")}</dt>
                              <dd>{currencyFormat.format(item.unitPricePerSqm)}/m²</dd>
                            </div>
                          )}
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("quotes.field.subtotal")}</dt>
                            <dd>{currencyFormat.format(item.subtotal)}</dd>
                          </div>
                          {item.discountPercent > 0 && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.discount")}</dt>
                              <dd>-{item.discountPercent}%</dd>
                            </div>
                          )}
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("quotes.field.vat")}</dt>
                            <dd>{item.vatPercent}%</dd>
                          </div>
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("quotes.field.grandTotal")}</dt>
                            <dd className="font-semibold text-primary">{currencyFormat.format(item.grandTotal)}</dd>
                          </div>
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("quotes.field.validUntil")}</dt>
                            <dd>{new Date(item.validUntil).toLocaleDateString()}</dd>
                          </div>
                          {item.ownerName && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.owner")}</dt>
                              <dd>{item.ownerName}</dd>
                            </div>
                          )}
                          {item.submittedAt && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.submittedAt")}</dt>
                              <dd>{new Date(item.submittedAt).toLocaleDateString()}</dd>
                            </div>
                          )}
                          {item.approvedAt && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.approvedAt")}</dt>
                              <dd>{new Date(item.approvedAt).toLocaleDateString()}</dd>
                            </div>
                          )}
                          {item.sentAt && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("quotes.field.sentAt")}</dt>
                              <dd>{new Date(item.sentAt).toLocaleDateString()}</dd>
                            </div>
                          )}
                        </dl>

                        {item.packageDescription && (
                          <div className="mb-3 text-sm">
                            <p className="text-muted-foreground text-xs mb-1">{t("quotes.field.packageDescription")}</p>
                            <p className="whitespace-pre-wrap">{item.packageDescription}</p>
                          </div>
                        )}

                        {item.method === "Boq" && item.items.length > 0 && (
                          <div className="mt-3 border-t pt-3">
                            <p className="text-xs font-medium text-muted-foreground mb-2">{t("quotes.items")} ({item.items.length})</p>
                            <div className="overflow-x-auto">
                              <table className="w-full text-xs">
                                <thead>
                                  <tr className="border-b text-left text-muted-foreground">
                                    <th className="pb-1 pr-2">{t("quotes.item.name")}</th>
                                    <th className="pb-1 pr-2 text-right">{t("quotes.item.quantity")}</th>
                                    <th className="pb-1 pr-2">{t("quotes.item.unit")}</th>
                                    <th className="pb-1 pr-2 text-right">{t("quotes.item.unitPrice")}</th>
                                    <th className="pb-1 text-right">{t("quotes.item.amount")}</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {item.items.map(line => (
                                    <tr key={line.id} className="border-b border-dashed">
                                      <td className="py-1 pr-2">
                                        {line.itemCode && <span className="text-muted-foreground">{line.itemCode} - </span>}
                                        {line.name}
                                      </td>
                                      <td className="py-1 pr-2 text-right">{line.quantity}</td>
                                      <td className="py-1 pr-2">{line.unit}</td>
                                      <td className="py-1 pr-2 text-right">{currencyFormat.format(line.unitPrice)}</td>
                                      <td className="py-1 text-right font-medium">{currencyFormat.format(line.amount)}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            </div>
                          </div>
                        )}

                        {item.note && (
                          <div className="mt-3 border-t pt-3 text-sm">
                            <p className="text-muted-foreground text-xs mb-1">{t("quotes.field.note")}</p>
                            <p className="whitespace-pre-wrap text-muted-foreground">{item.note}</p>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </AccordionContent>
            </AccordionItem>

            <AccordionItem value="contracts" className="rounded-lg border bg-card px-4">
              <AccordionTrigger className="hover:no-underline py-4">
                <div className="flex items-center gap-3">
                  <ShoppingCart className="h-4 w-4 text-muted-foreground" />
                  <span className="font-semibold">{t("operationalProjects.related.contracts")}</span>
                  <Badge variant="secondary" className="ml-1">{detail.contracts.length}</Badge>
                </div>
              </AccordionTrigger>
              <AccordionContent className="pb-4">
                {detail.contracts.length === 0 ? (
                  <p className="py-2 text-sm text-muted-foreground">{t("operationalProjects.related.empty")}</p>
                ) : (
                  <div className="space-y-4">
                    {detail.contracts.map(item => (
                      <div key={item.id} className="rounded-md border p-4">
                        <div className="flex items-start justify-between gap-3 mb-3">
                          <div className="flex items-center gap-2 flex-wrap">
                            <p className="font-medium font-mono">{item.contractNumber}</p>
                            <Badge variant="outline" className="text-xs">{t(`contracts.status.${item.status}`)}</Badge>
                          </div>
                          <Button variant="ghost" size="sm" asChild>
                            <Link to={`/admin/contracts/${item.id}`}><ExternalLink className="h-4 w-4" /></Link>
                          </Button>
                        </div>

                        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
                          <div>
                            <dt className="text-muted-foreground text-xs">{t("contracts.field.value")}</dt>
                            <dd className="font-semibold text-primary">{currencyFormat.format(item.value)}</dd>
                          </div>
                          {item.signedDate && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("contracts.field.signedDate")}</dt>
                              <dd>{new Date(item.signedDate).toLocaleDateString()}</dd>
                            </div>
                          )}
                          {(item.startDate || item.endDate) && (
                            <div className="col-span-2">
                              <dt className="text-muted-foreground text-xs">{t("contracts.field.duration")}</dt>
                              <dd>
                                {item.startDate && new Date(item.startDate).toLocaleDateString()}
                                {item.startDate && item.endDate && " - "}
                                {item.endDate && new Date(item.endDate).toLocaleDateString()}
                              </dd>
                            </div>
                          )}
                          {item.ownerName && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("contracts.field.owner")}</dt>
                              <dd>{item.ownerName}</dd>
                            </div>
                          )}
                          {item.customerName && (
                            <div>
                              <dt className="text-muted-foreground text-xs">{t("contracts.field.customer")}</dt>
                              <dd>{item.customerName}</dd>
                            </div>
                          )}
                        </dl>

                        {item.scopeOfWork && (
                          <div className="mt-3 border-t pt-3 text-sm">
                            <p className="text-muted-foreground text-xs mb-1">{t("contracts.field.scopeOfWork")}</p>
                            <div className="whitespace-pre-wrap text-sm" dangerouslySetInnerHTML={{ __html: item.scopeOfWork }} />
                          </div>
                        )}

                        {item.note && (
                          <div className="mt-3 border-t pt-3 text-sm">
                            <p className="text-muted-foreground text-xs mb-1">{t("contracts.field.note")}</p>
                            <p className="whitespace-pre-wrap text-muted-foreground">{item.note}</p>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </AccordionContent>
            </AccordionItem>
          </Accordion>
        </div>
      ) : (
        <div className="space-y-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div><h1 className="text-2xl font-semibold">{t("operationalProjects.title")}</h1><p className="mt-1 text-sm text-muted-foreground">{t("operationalProjects.subtitle")}</p></div>
            {canManage && <Button onClick={openCreate}><Plus className="mr-2 h-4 w-4" />{t("operationalProjects.new")}</Button>}
          </div>
          <div className="flex flex-col gap-3 rounded-lg border bg-card p-4 sm:flex-row sm:items-end">
            <div className="flex-1"><Label htmlFor="project-search">{t("operationalProjects.filter.search")}</Label><div className="relative mt-1"><Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" /><Input id="project-search" className="pl-9" value={search} onChange={event => setSearch(event.target.value)} placeholder={t("operationalProjects.filter.searchPlaceholder")} /></div></div>
            <div className="sm:w-56"><Label>{t("operationalProjects.field.status")}</Label><Select value={status || "all"} onValueChange={value => setStatus(value === "all" ? "" : value as OperationalProjectStatus)}><SelectTrigger className="mt-1"><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("operationalProjects.filter.allStatuses")}</SelectItem>{OPERATIONAL_PROJECT_STATUSES.map(item => <SelectItem key={item} value={item}>{t(`operationalProjects.status.${item}`)}</SelectItem>)}</SelectContent></Select></div>
            <Button variant="outline" onClick={() => void load()}><RefreshCcw className="mr-2 h-4 w-4" />{t("common.refresh")}</Button>
          </div>

          <p className="text-sm text-muted-foreground">{t("operationalProjects.total", { count: total })}</p>
          {rows.length === 0 ? (
            <div className="rounded-lg border border-dashed p-10 text-center text-muted-foreground">{t("operationalProjects.empty")}</div>
          ) : (
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {rows.map(project => (
                <div key={project.id} className="relative rounded-lg border bg-card p-4 text-left transition hover:border-primary/50 hover:shadow-sm">
                  <button
                    type="button"
                    className="w-full text-left"
                    onClick={() => navigate(`/admin/operational-projects/${project.id}`)}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-mono text-xs text-muted-foreground">{project.code}</p>
                        <h2 className="mt-1 font-semibold">{project.name}</h2>
                      </div>
                      <Badge variant="outline" className={statusClass[project.status]}>{t(`operationalProjects.status.${project.status}`)}</Badge>
                    </div>
                    <p className="mt-2 text-sm text-muted-foreground">{project.customerName}</p>
                    <div className="mt-4 grid grid-cols-3 gap-2 border-t pt-3 text-center text-xs">
                      <Count value={project.opportunityCount} label={t("operationalProjects.count.opportunities")} />
                      <Count value={project.quoteCount} label={t("operationalProjects.count.quotes")} />
                      <Count value={project.contractCount} label={t("operationalProjects.count.contracts")} />
                    </div>
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader><DialogTitle>{t(detail ? "operationalProjects.edit" : "operationalProjects.new")}</DialogTitle><DialogDescription>{t("operationalProjects.formHint")}</DialogDescription></DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2"><Label htmlFor="project-name">{t("operationalProjects.field.name")}</Label><Input id="project-name" maxLength={300} value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} /></div>
            <div><Label>{t("operationalProjects.field.customer")}</Label><Select value={form.customerId ? String(form.customerId) : ""} onValueChange={value => setForm(current => ({ ...current, customerId: Number(value) }))}><SelectTrigger><SelectValue placeholder={t("operationalProjects.selectCustomer")} /></SelectTrigger><SelectContent>{customers.map(customer => <SelectItem key={customer.id} value={String(customer.id)}>{customer.name}</SelectItem>)}</SelectContent></Select></div>
            <div><Label>{t("operationalProjects.field.manager")}</Label><Select value={form.projectManagerUserId ? String(form.projectManagerUserId) : "self"} onValueChange={value => setForm(current => ({ ...current, projectManagerUserId: value === "self" ? null : Number(value) }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="self">{t("operationalProjects.managerSelf")}</SelectItem>{users.map(user => <SelectItem key={user.id} value={String(user.id)}>{user.fullName || user.email || user.phoneNumber}</SelectItem>)}</SelectContent></Select></div>
            {detail && <div className="sm:col-span-2"><Label>{t("operationalProjects.field.status")}</Label><Select value={form.status} onValueChange={value => setForm(current => ({ ...current, status: value as OperationalProjectStatus }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{OPERATIONAL_PROJECT_STATUSES.map(item => <SelectItem key={item} value={item}>{t(`operationalProjects.status.${item}`)}</SelectItem>)}</SelectContent></Select></div>}
            <div><Label htmlFor="project-start">{t("operationalProjects.field.startDate")}</Label><Input id="project-start" type="date" value={dateInput(form.startDate)} onChange={event => setForm(current => ({ ...current, startDate: event.target.value }))} /></div>
            <div><Label htmlFor="project-end">{t("operationalProjects.field.endDate")}</Label><Input id="project-end" type="date" value={dateInput(form.endDate)} onChange={event => setForm(current => ({ ...current, endDate: event.target.value }))} /></div>
            <div className="sm:col-span-2"><Label htmlFor="project-note">{t("operationalProjects.field.note")}</Label><Textarea id="project-note" maxLength={4000} value={form.note ?? ""} onChange={event => setForm(current => ({ ...current, note: event.target.value }))} /></div>
          </div>
          {formError && <p role="alert" className="text-sm text-destructive">{formError}</p>}
          <DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>{t("common.cancel")}</Button><Button disabled={saving} onClick={() => void save()}>{saving ? t("common.saving") : t("common.save")}</Button></DialogFooter>
        </DialogContent>
      </Dialog>

      <DeletionImpactDialog
        open={deleteOpen}
        impact={deleteImpact}
        loading={deleteImpactLoading}
        deleting={deleting}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !deleting) {
            setDeleteOpen(false);
            setDeleteImpact(null);
            setDeleteError(null);
          }
        }}
        onConfirm={remove}
        onCompleted={completeDelete}
      />
    </AdminLayout>
  );
};

const Summary = ({ label, value, href }: { label: string; value: string; href?: string }) => <div className="rounded-lg border bg-card p-4"><p className="text-xs text-muted-foreground">{label}</p>{href ? <Link className="mt-1 block font-medium text-primary hover:underline" to={href}>{value}</Link> : <p className="mt-1 font-medium">{value}</p>}</div>;
const Count = ({ value, label }: { value: number; label: string }) => <div><strong className="block text-base">{value}</strong><span className="text-muted-foreground">{label}</span></div>;

export default OperationalProjects;
