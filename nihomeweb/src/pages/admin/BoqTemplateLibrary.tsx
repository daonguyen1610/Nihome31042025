import { useCallback, useEffect, useState } from "react";
import {
  ChevronDown,
  ChevronRight,
  Download,
  FileSpreadsheet,
  FolderOpen,
  Loader2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { MaterialRatesNav } from "@/components/admin/MaterialRatesNav";
import { PageError, PageLoading } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { saveBlob } from "@/lib/boqWorkbook";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  adminApi,
  type BoqLibrarySheetOutline,
  type BoqLibraryTemplate,
} from "@/services/adminApi";

type Selection = { code: string; sheet?: string };

// Read-only library of the customer-format BOQ forms: one folder per form, one child per sheet.
// Procurement downloads a form here and uploads the filled file in the input-price work package.
const BoqTemplateLibrary = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [templates, setTemplates] = useState<BoqLibraryTemplate[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [selection, setSelection] = useState<Selection | null>(null);
  const [outline, setOutline] = useState<BoqLibrarySheetOutline | null>(null);
  const [outlineError, setOutlineError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await adminApi.listBoqLibrary();
      setTemplates(data);
      if (data[0]) {
        setExpanded(new Set([data[0].code]));
        setSelection({ code: data[0].code });
      }
    } catch (reason) {
      setError(extractApiError(reason));
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setOutline(null);
    setOutlineError(null);
    if (!selection?.sheet) return;
    let active = true;
    adminApi
      .getBoqLibrarySheet(selection.code, selection.sheet)
      .then(({ data }) => active && setOutline(data))
      .catch((reason) => active && setOutlineError(extractApiError(reason)));
    return () => {
      active = false;
    };
  }, [selection]);

  const toggle = (code: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });

  const download = async (template: BoqLibraryTemplate) => {
    setDownloading(true);
    try {
      const { data } = await adminApi.downloadBoqLibraryFile(template.code);
      saveBlob(data, template.file);
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) });
    } finally {
      setDownloading(false);
    }
  };

  const selected = templates?.find((template) => template.code === selection?.code);

  return (
    <AdminLayout>
      <div className="space-y-4" data-testid="boq-library">
        <header>
          <h1 className="text-2xl font-semibold tracking-tight">{t("boqLibrary.title")}</h1>
          <p className="text-sm text-muted-foreground">{t("boqLibrary.subtitle")}</p>
        </header>
        <MaterialRatesNav active="boq" />
        <ol className="grid gap-2 rounded-lg border bg-primary/5 p-3 text-sm sm:grid-cols-3">
          {["download", "fill", "upload"].map((step, index) => (
            <li key={step} className="flex gap-2">
              <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-semibold text-primary-foreground">
                {index + 1}
              </span>
              <span>{t(`boqLibrary.step.${step}`)}</span>
            </li>
          ))}
        </ol>

        {error ? (
          <PageError message={error} onRetry={() => void load()} />
        ) : !templates ? (
          <PageLoading />
        ) : (
          <div className="grid gap-4 lg:grid-cols-[minmax(260px,340px)_minmax(0,1fr)]">
            <nav
              className="rounded-lg border bg-card p-2"
              aria-label={t("boqLibrary.tree")}
              data-testid="boq-library-tree"
            >
              <ul role="tree" className="space-y-1">
                {templates.map((template) => {
                  const open = expanded.has(template.code);
                  return (
                    <li key={template.code} role="treeitem" aria-expanded={open}>
                      <div
                        className={cn(
                          "flex items-center gap-1 rounded-md",
                          selection?.code === template.code && !selection.sheet && "bg-primary/10",
                        )}
                      >
                        <button
                          type="button"
                          className="rounded p-1 text-muted-foreground hover:bg-muted"
                          aria-label={t(open ? "boqLibrary.collapse" : "boqLibrary.expand")}
                          onClick={() => toggle(template.code)}
                        >
                          {open ? (
                            <ChevronDown className="h-4 w-4" />
                          ) : (
                            <ChevronRight className="h-4 w-4" />
                          )}
                        </button>
                        <button
                          type="button"
                          className="flex min-w-0 flex-1 items-center gap-2 py-1.5 pr-2 text-left text-sm font-medium"
                          data-testid={`boq-library-template-${template.code}`}
                          onClick={() => {
                            setSelection({ code: template.code });
                            setExpanded((current) => new Set(current).add(template.code));
                          }}
                        >
                          <FolderOpen className="h-4 w-4 shrink-0 text-primary" />
                          <span className="break-words">{t(template.nameKey)}</span>
                        </button>
                      </div>
                      {open && (
                        <ul role="group" className="ml-7 mt-1 space-y-0.5 border-l pl-2">
                          {template.sheets.map((sheet) => (
                            <li key={sheet.name} role="treeitem">
                              <button
                                type="button"
                                className={cn(
                                  "flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-muted",
                                  selection?.code === template.code &&
                                    selection.sheet === sheet.name &&
                                    "bg-primary/10 font-medium",
                                )}
                                data-testid={`boq-library-sheet-${template.code}-${sheet.name}`}
                                onClick={() =>
                                  setSelection({ code: template.code, sheet: sheet.name })
                                }
                              >
                                <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" />
                                <span className="min-w-0 flex-1 break-words">{sheet.name}</span>
                                <span className="shrink-0 text-xs text-muted-foreground">
                                  {sheet.isData
                                    ? t("boqLibrary.itemCount", { count: sheet.itemCount })
                                    : t("boqLibrary.summarySheet")}
                                </span>
                              </button>
                            </li>
                          ))}
                        </ul>
                      )}
                    </li>
                  );
                })}
              </ul>
              {templates.length === 0 && (
                <p className="p-3 text-sm text-muted-foreground">{t("boqLibrary.empty")}</p>
              )}
            </nav>

            {selected && (
              <section className="min-w-0 space-y-4 rounded-lg border bg-card p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-xs text-muted-foreground">
                      {selection?.sheet
                        ? `${t(selected.nameKey)} / ${selection.sheet}`
                        : selected.file}
                    </p>
                    <h2 className="break-words text-lg font-semibold">
                      {selection?.sheet ?? t(selected.nameKey)}
                    </h2>
                  </div>
                  <Button
                    onClick={() => void download(selected)}
                    disabled={downloading}
                    data-testid="boq-library-download"
                  >
                    {downloading ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Download className="mr-2 h-4 w-4" />
                    )}
                    {t("boqLibrary.download")}
                  </Button>
                </div>

                {!selection?.sheet ? (
                  <ul className="divide-y rounded-md border text-sm">
                    {selected.sheets.map((sheet) => (
                      <li key={sheet.name}>
                        <button
                          type="button"
                          className="flex w-full items-center justify-between gap-3 p-3 text-left hover:bg-muted/50"
                          onClick={() => setSelection({ code: selected.code, sheet: sheet.name })}
                        >
                          <span className="flex min-w-0 items-center gap-2">
                            <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" />
                            <span className="break-words font-medium">{sheet.name}</span>
                          </span>
                          <Badge variant="outline" className="shrink-0">
                            {sheet.isData
                              ? t("boqLibrary.itemCount", { count: sheet.itemCount })
                              : t("boqLibrary.summarySheet")}
                          </Badge>
                        </button>
                      </li>
                    ))}
                  </ul>
                ) : outlineError ? (
                  <p role="alert" className="text-sm text-destructive">
                    {outlineError}
                  </p>
                ) : !outline ? (
                  <PageLoading />
                ) : !outline.isData ? (
                  <p className="rounded-md bg-muted/40 p-3 text-sm text-muted-foreground">
                    {t("boqLibrary.summaryHint")}
                  </p>
                ) : (
                  <div className="space-y-2">
                    <p className="text-xs text-muted-foreground">{t("boqLibrary.outlineHint")}</p>
                    <ol
                      className="divide-y rounded-md border text-sm"
                      data-testid="boq-library-outline"
                    >
                      {outline.rows.map((row) => (
                        <li
                          key={row.row}
                          className={cn(
                            "grid grid-cols-[3rem_minmax(0,1fr)_auto] gap-2 px-3 py-1.5",
                            row.kind === "group" && "bg-muted/40 font-semibold",
                            row.kind === "subtotal" && "italic text-muted-foreground",
                            row.kind === "total" && "bg-primary/5 font-semibold",
                          )}
                        >
                          <span className="text-muted-foreground">{row.stt}</span>
                          <span className="break-words">{row.description}</span>
                          <span className="text-muted-foreground">{row.unit}</span>
                        </li>
                      ))}
                    </ol>
                  </div>
                )}
              </section>
            )}
          </div>
        )}
      </div>
    </AdminLayout>
  );
};

export default BoqTemplateLibrary;
