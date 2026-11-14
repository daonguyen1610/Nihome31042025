import { useCallback, useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { ExternalLink, FileUp } from "lucide-react";
import { BoqWorkbookUploadDialog } from "@/components/admin/BoqWorkbookUploadDialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { useI18n } from "@/lib/i18n";
import {
  adminApi,
  type ContractResponse,
  type ProjectBoqRevisionListItemResponse,
} from "@/services/adminApi";

// The project's root BOQ. Procurement uploads the tender BOQ in the input-price work package and
// the package lead approves it; the PM turns it into the contract BOQ on its detail page. A
// contract BOQ that differs from the tender (or a later change) is uploaded here once signed.
export function ProjectBoqSection({ projectId }: { projectId: number }) {
  const { t, lang } = useI18n();
  const { has } = usePermissions();
  const navigate = useNavigate();
  const { toast } = useToast();
  const { hash } = useLocation();
  const canUpload = has(ADMIN_PERMS.procurementManage) && has(ADMIN_PERMS.contracts);
  const [rows, setRows] = useState<ProjectBoqRevisionListItemResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [uploadOpen, setUploadOpen] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await adminApi.listProjectBoqRevisions(projectId, {
        sortBy: "revision",
        sortDirection: "desc",
        page: 1,
        pageSize: 20,
      });
      setRows(data.items);
    } catch (reason) {
      setError(extractApiError(reason));
    }
  }, [projectId]);

  useEffect(() => {
    setRows(null);
    void load();
  }, [load]);

  useEffect(() => {
    if (!canUpload) return;
    adminApi
      .listContracts({ page: 1, pageSize: 200, operationalProjectId: projectId })
      .then(({ data }) =>
        setContracts(
          data.items.filter(
            (contract) =>
              contract.direction === "Upstream" &&
              contract.signedDate &&
              !["Draft", "Cancelled"].includes(contract.status),
          ),
        ),
      )
      .catch(() => setContracts([]));
  }, [canUpload, projectId]);

  // Quotes link here with #project-boq; the section renders after the project loads.
  useEffect(() => {
    if (hash === "#project-boq" && rows)
      document.getElementById("project-boq")?.scrollIntoView({ block: "start" });
  }, [hash, rows]);

  const detailPath = (id: number) => `/admin/procurement-control/projects/${projectId}/boq/${id}`;
  const money = (row: ProjectBoqRevisionListItemResponse) =>
    new Intl.NumberFormat(lang, {
      style: "currency",
      currency: row.currency,
      maximumFractionDigits: 0,
    }).format(row.costTotal);

  return (
    <section
      id="project-boq"
      data-testid="project-boq"
      className="scroll-mt-4 space-y-4 rounded-lg border bg-card p-4 shadow-sm"
    >
      <header className="flex flex-row flex-wrap items-center justify-between gap-3">
        <div className="min-w-0 flex-[1_1_18rem]">
          <h2 className="text-lg font-semibold">{t("boqWorkbook.projectSection.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("boqWorkbook.projectSection.hint")}
          </p>
        </div>
        {canUpload && contracts.length > 0 && (
          <Button
            variant="outline"
            onClick={() => setUploadOpen(true)}
            data-testid="boq-contract-upload"
          >
            <FileUp className="mr-2 h-4 w-4" />
            {t("boqWorkbook.contractUpload")}
          </Button>
        )}
      </header>
      {error ? (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm"
        >
          <span>{error}</span>
          <Button size="sm" variant="outline" onClick={() => void load()}>
            {t("common.retry")}
          </Button>
        </div>
      ) : !rows ? (
        <p className="text-sm text-muted-foreground">{t("common.loading")}</p>
      ) : rows.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("procurement.boq.empty")}</p>
      ) : (
        <ul className="divide-y rounded-md border">
          {rows.map((row) => (
            <li
              key={row.id}
              className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 p-3 text-sm"
            >
              <div className="flex min-w-0 flex-wrap items-center gap-2">
                <span className="font-medium">R{row.revisionNumber}</span>
                <Badge variant="outline">{t(`procurement.status.${row.status}`)}</Badge>
                <span className="text-muted-foreground">
                  {row.upstreamContractNumber ?? t("boqWorkbook.tenderStage")}
                </span>
              </div>
              <div className="flex items-center gap-3">
                <span className="tabular-nums">{money(row)}</span>
                <Button variant="ghost" size="sm" asChild>
                  <Link to={detailPath(row.id)} aria-label={t("common.view")}>
                    <ExternalLink className="h-4 w-4" />
                  </Link>
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <BoqWorkbookUploadDialog
        target={{ kind: "project", projectId, contracts }}
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        onCreated={(revision) => {
          setUploadOpen(false);
          toast({ title: t("boqWorkbook.created") });
          navigate(detailPath(revision.id));
        }}
      />
    </section>
  );
}
