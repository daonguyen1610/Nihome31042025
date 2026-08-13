import { useCallback, useEffect, useMemo, useState } from "react";
import { DraftingCompass, FileText, History, Lightbulb, Loader2, PackageCheck, RefreshCw, Ruler } from "lucide-react";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { extractApiError } from "@/lib/apiError";
import {
  adminApi,
  type BasicDesignDocResponse,
  type ConceptOptionResponse,
  type DesignProjectResponse,
  type DrawingRevisionResponse,
  type IfcReleaseResponse,
  type ShopDrawingResponse,
} from "@/services/adminApi";

interface Props {
  project: DesignProjectResponse;
}

interface DocumentsState {
  concepts: ConceptOptionResponse[];
  basicDocs: BasicDesignDocResponse[];
  shopDrawings: ShopDrawingResponse[];
  revisions: DrawingRevisionResponse[];
  ifcReleases: IfcReleaseResponse[];
}

interface DocumentRow {
  id: number;
  code: string;
  title: string;
  status: string;
  filePath?: string | null;
  originalFileName?: string | null;
}

const EMPTY_STATE: DocumentsState = {
  concepts: [],
  basicDocs: [],
  shopDrawings: [],
  revisions: [],
  ifcReleases: [],
};

export const DesignProjectDocumentsTab = ({ project }: Props) => {
  const { t } = useI18n();
  const [data, setData] = useState<DocumentsState>(EMPTY_STATE);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [concepts, basicDocs, shopDrawings, revisions, ifcReleases] = await Promise.all([
        adminApi.listConceptOptions({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listBasicDesignDocs({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listShopDrawings({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listDrawingRevisions({ designProjectId: project.id, pageSize: 200 }),
        adminApi.listIfcReleases({ designProjectId: project.id, pageSize: 200 }),
      ]);
      setData({
        concepts: concepts.data.items,
        basicDocs: basicDocs.data.items,
        shopDrawings: shopDrawings.data.items,
        revisions: revisions.data.items,
        ifcReleases: ifcReleases.data.items,
      });
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [project.id]);

  useEffect(() => {
    void load();
  }, [load]);

  const total = useMemo(
    () => data.concepts.length + data.basicDocs.length + data.shopDrawings.length + data.revisions.length + data.ifcReleases.length,
    [data],
  );

  const groups: Array<{ key: string; title: string; icon: typeof FileText; rows: DocumentRow[] }> = [
    {
      key: "concepts",
      title: t("designProjects.documents.group.concepts"),
      icon: Lightbulb,
      rows: data.concepts.map((item) => ({ id: item.id, code: `#${item.id}`, title: item.name, status: t(`concepts.status.${item.status}`) })),
    },
    {
      key: "basic",
      title: t("designProjects.documents.group.basic"),
      icon: Ruler,
      rows: data.basicDocs.map((item) => ({ id: item.id, code: item.documentCode, title: item.title, status: t(`basicDesign.status.${item.status}`), filePath: item.filePath, originalFileName: item.originalFileName })),
    },
    {
      key: "shop",
      title: t("designProjects.documents.group.shop"),
      icon: DraftingCompass,
      rows: data.shopDrawings.map((item) => ({ id: item.id, code: item.drawingCode, title: item.title, status: t(`shopDrawing.status.${item.status}`), filePath: item.filePath, originalFileName: item.originalFileName })),
    },
    {
      key: "revisions",
      title: t("designProjects.documents.group.revisions"),
      icon: History,
      rows: data.revisions.map((item) => ({ id: item.id, code: `${item.targetCode ?? `#${item.targetId}`} · ${item.revisionLabel}`, title: item.targetTitle ?? item.note, status: item.reasonLabel ?? item.reasonCode })),
    },
    {
      key: "ifc",
      title: t("designProjects.documents.group.ifc"),
      icon: PackageCheck,
      rows: data.ifcReleases.map((item) => ({ id: item.id, code: item.releaseNumber, title: item.title, status: t(`ifcRelease.status.${item.status}`) })),
    },
  ];

  return (
    <section className="space-y-4" data-testid="design-project-documents-tab">
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 text-base font-semibold text-slate-900">
              <FileText className="h-4 w-4 text-slate-500" />
              {t("designProjects.documents.title")}
            </h2>
            <p className="mt-1 text-sm text-slate-500">{t("designProjects.documents.description")}</p>
          </div>
          {!loading && !error ? (
            <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-700">
              {total} {t("designProjects.documents.recordCount")}
            </Badge>
          ) : null}
        </div>

        {loading ? (
          <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-slate-400" /></div>
        ) : error ? (
          <div className="mt-4 rounded-md border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
            <p>{error}</p>
            <Button variant="outline" size="sm" className="mt-3" onClick={() => void load()}>
              <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
              {t("common.retry")}
            </Button>
          </div>
        ) : total === 0 ? (
          <div className="mt-4 rounded-md border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500">
            {t("designProjects.documents.empty")}
          </div>
        ) : null}
      </div>

      {!loading && !error && total > 0 ? (
        <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
          {groups.map(({ key, title, icon: Icon, rows }) => (
            <section key={key} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between gap-2">
                <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                  <Icon className="h-4 w-4 text-slate-500" />
                  {title}
                </h3>
                <Badge variant="secondary">{rows.length}</Badge>
              </div>
              {rows.length === 0 ? (
                <p className="py-6 text-center text-sm text-slate-400">{t("designProjects.documents.groupEmpty")}</p>
              ) : (
                <ul className="mt-3 divide-y divide-slate-100">
                  {rows.map((row) => (
                    <li key={row.id} className="flex flex-wrap items-start justify-between gap-2 py-2.5 first:pt-0 last:pb-0">
                      <div className="min-w-0">
                        <p className="font-mono text-xs text-slate-500">{row.code}</p>
                        <p className="truncate text-sm font-medium text-slate-900">{row.title}</p>
                      </div>
                      <div className="flex items-center gap-1">
                        <Badge variant="outline" className="max-w-full whitespace-normal text-right text-xs font-normal text-slate-600">
                          {row.status}
                        </Badge>
                        {row.filePath ? (
                          <AdminFilePreview url={row.filePath} fileName={row.originalFileName} variant="ghost" className="h-7 w-7" />
                        ) : null}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          ))}
        </div>
      ) : null}
    </section>
  );
};