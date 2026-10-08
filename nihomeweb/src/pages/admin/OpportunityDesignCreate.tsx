import { useEffect, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, Plus } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { PageError, PageLoading } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type OpportunityResponse,
  type OperationalProjectListItemResponse,
} from "@/services/adminApi";

const OpportunityDesignCreate = () => {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const opportunityId = Number(id);
  const requestedProjectId = Number(searchParams.get("projectId"));
  const invalidOpportunity = !Number.isInteger(opportunityId) || opportunityId < 1;
  const backPath = invalidOpportunity ? "/admin/opportunities" : `/admin/opportunities/${opportunityId}`;
  const [opportunity, setOpportunity] = useState<OpportunityResponse | null>(null);
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [projectId, setProjectId] = useState<number | null>(null);
  const [name, setName] = useState("");
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (invalidOpportunity) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        const { data } = await adminApi.getOpportunity(opportunityId);
        const items: OperationalProjectListItemResponse[] = [];
        let page = 1;
        let total = 0;
        do {
          const result = await adminApi.listOperationalProjects({
            customerId: data.customerId, page, pageSize: 100,
          });
          items.push(...result.data.items);
          total = result.data.total;
          page += 1;
          if (result.data.items.length === 0) break;
        } while (items.length < total);
        if (cancelled) return;
        setOpportunity(data);
        setProjects(items);
        setName(data.name);
        setProjectId(data.operationalProjectId ??
          (items.some((project) => project.id === requestedProjectId) ? requestedProjectId : null));
      } catch (reason) {
        if (!cancelled) setError(extractApiError(reason));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [opportunityId, requestedProjectId, invalidOpportunity]);

  const createProjectPath = opportunity
    ? `/admin/operational-projects?create=1&customerId=${opportunity.customerId}&returnOpportunity=${opportunity.id}`
    : "/admin/operational-projects";

  const submit = async () => {
    if (!opportunity || saving) return;
    const trimmedName = name.trim();
    if (!trimmedName || trimmedName.length > 300) {
      setFormError(t("opportunities.designProject.nameRule"));
      return;
    }
    if (!projectId) {
      setFormError(t("opportunities.designProject.projectRule"));
      return;
    }
    if (note.length > 4000) {
      setFormError(t("opportunities.designProject.noteRule"));
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      if (opportunity.operationalProjectId !== projectId) {
        const updated = await adminApi.updateOpportunity(opportunity.id, {
          rowVersion: opportunity.rowVersion,
          name: opportunity.name,
          customerId: opportunity.customerId,
          operationalProjectId: projectId,
          ownerUserId: opportunity.ownerUserId,
          estimatedValue: opportunity.estimatedValue,
          winProbability: opportunity.winProbability,
          expectedCloseDate: opportunity.expectedCloseDate,
          note: opportunity.note,
        });
        setOpportunity(updated.data);
      }
      const { data } = await adminApi.startOpportunityDesign(opportunity.id, {
        name: trimmedName,
        note: note.trim() || null,
      });
      toast({ title: t(data.created
        ? "opportunities.designProject.created"
        : "opportunities.designProject.alreadyExists") });
      navigate(backPath);
    } catch (reason) {
      setFormError(extractApiError(reason));
    } finally {
      setSaving(false);
    }
  };

  return (
    <AdminLayout>
      <div className="mx-auto max-w-2xl space-y-5 px-2 py-4 sm:px-0">
        <Button variant="ghost" asChild><Link to={backPath}><ArrowLeft className="mr-2 h-4 w-4" />{t("common.back")}</Link></Button>
        {invalidOpportunity ? <PageError message={t("opportunities.designProject.invalidOpportunity")} />
          : loading ? <PageLoading /> : error ? <PageError message={error} /> : opportunity && (
          <div className="space-y-5 rounded-lg border bg-card p-4 sm:p-6" data-testid="opportunity-design-form">
            <div>
              <h1 className="text-2xl font-semibold">{t("opportunities.action.createDesignProject")}</h1>
              <p className="text-sm text-muted-foreground">{t("opportunities.designProject.formHint")}</p>
            </div>
            {opportunity.stage === "Lost" && <p role="alert" className="text-sm text-destructive">{t("opportunities.designProject.lost")}</p>}
            {opportunity.designProjectId && <p role="status" className="text-sm text-muted-foreground">{t("opportunities.designProject.alreadyExists")}</p>}
            <div>
              <Label>{t("opportunities.field.customer")}</Label>
              <Input value={opportunity.customerName ?? String(opportunity.customerId)} disabled />
            </div>
            <div>
              <Label htmlFor="design-project-name">{t("designProjects.field.name")}</Label>
              <Input id="design-project-name" value={name} maxLength={300} onChange={(event) => setName(event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>{t("opportunities.field.operationalProject")}</Label>
              <SearchableSelect
                value={projectId ? String(projectId) : null}
                onChange={(value) => setProjectId(Number(value))}
                options={projects.map((project) => ({ value: String(project.id), label: `${project.code} · ${project.name}` }))}
                disabled={!!opportunity.operationalProjectId}
                ariaLabel={t("opportunities.field.operationalProject")}
                placeholder={t("opportunities.project.select")}
              />
              {!opportunity.operationalProjectId && (
                <Button variant="link" className="h-auto p-0" asChild>
                  <Link to={createProjectPath}><Plus className="mr-1 h-4 w-4" />{t("opportunities.project.create")}</Link>
                </Button>
              )}
            </div>
            <div>
              <Label htmlFor="design-project-note">{t("designProjects.field.note")}</Label>
              <Textarea id="design-project-note" rows={3} maxLength={4000} value={note} onChange={(event) => setNote(event.target.value)} />
            </div>
            {formError && <p role="alert" className="text-sm text-destructive">{formError}</p>}
            <div className="flex flex-wrap justify-end gap-2">
              <Button variant="outline" asChild><Link to={backPath}>{t("common.cancel")}</Link></Button>
              <Button data-testid="opportunity-design-submit" disabled={saving || opportunity.stage === "Lost" || !!opportunity.designProjectId} onClick={() => void submit()}>
                {t("common.save")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
};

export default OpportunityDesignCreate;
