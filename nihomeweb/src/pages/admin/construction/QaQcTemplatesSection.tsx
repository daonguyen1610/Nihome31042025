import { useEffect, useState, type FormEvent } from "react";
import { ClipboardCheck, FilePlus2, Plus, Trash2 } from "lucide-react";
import { PageEmpty } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type QaQcTemplateResponse } from "@/services/adminApi";
import {
  parseQaQcTemplateSchema,
  serializeQaQcTemplateSchema,
  type QaQcTemplateItem,
} from "./qaQcChecklist";

const nextChecklistItem = (items: QaQcTemplateItem[]): QaQcTemplateItem => {
  const usedCodes = new Set(items.map((item) => item.code.toLocaleUpperCase()));
  let number = items.length + 1;
  while (usedCodes.has(`C${number}`)) number += 1;
  return { code: `C${number}`, title: "", acceptanceCriteria: "" };
};

export function QaQcTemplatesSection({
  projectId,
  onChanged,
}: {
  projectId: number;
  onChanged?: () => void;
}) {
  const { t } = useI18n();
  const uiError = useUiError();
  const { toast } = useToast();
  const [templates, setTemplates] = useState<QaQcTemplateResponse[]>([]);
  const [open, setOpen] = useState(false);
  const [acting, setActing] = useState(false);
  const [form, setForm] = useState({
    code: "",
    nameVi: "",
    name: "",
    nameZh: "",
    nameJa: "",
  });
  const [checklistItems, setChecklistItems] = useState<QaQcTemplateItem[]>(() => [
    nextChecklistItem([]),
  ]);
  const [advancedJson, setAdvancedJson] = useState(() =>
    serializeQaQcTemplateSchema([nextChecklistItem([])]),
  );

  useEffect(() => {
    let active = true;
    void adminApi
      .listQaQcTemplates(projectId)
      .then(({ data }) => {
        if (active) setTemplates(data ?? []);
      })
      .catch(() => {
        /* non-fatal */
      });
    return () => {
      active = false;
    };
  }, [projectId]);

  const createTemplate = async (event: FormEvent) => {
    event.preventDefault();
    if (
      checklistItems.length === 0 ||
      checklistItems.some((item) => !item.title.trim() || !item.acceptanceCriteria.trim())
    ) {
      toast({ variant: "destructive", title: t("construction.qaQc.checklistRequired") });
      return;
    }
    setActing(true);
    try {
      const { data } = await adminApi.createQaQcTemplate(projectId, {
        ...form,
        schemaJson: serializeQaQcTemplateSchema(checklistItems),
      });
      setTemplates((current) => [...current, data]);
      setOpen(false);
      toast({ title: t("construction.qaQc.templateCreated") });
      onChanged?.();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
    } finally {
      setActing(false);
    }
  };

  const openCreate = () => {
    const initialItems = [nextChecklistItem([])];
    setForm({ code: "", nameVi: "", name: "", nameZh: "", nameJa: "" });
    setChecklistItems(initialItems);
    setAdvancedJson(serializeQaQcTemplateSchema(initialItems));
    setOpen(true);
  };

  const updateChecklistItem = (index: number, patch: Partial<QaQcTemplateItem>) => {
    const next = checklistItems.map((item, itemIndex) =>
      itemIndex === index ? { ...item, ...patch } : item,
    );
    setChecklistItems(next);
    setAdvancedJson(serializeQaQcTemplateSchema(next));
  };

  const applyAdvancedJson = () => {
    try {
      const items = parseQaQcTemplateSchema(advancedJson);
      if (items.length === 0) throw new Error("empty-checklist");
      setChecklistItems(items);
      setAdvancedJson(serializeQaQcTemplateSchema(items));
      toast({ title: t("construction.qaQc.advancedApplied") });
    } catch {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidSchemaJson") });
    }
  };

  const publishTemplate = async (template: QaQcTemplateResponse) => {
    try {
      const { data } = await adminApi.publishQaQcTemplate(
        projectId,
        template.id,
        template.rowVersion,
      );
      setTemplates((current) => current.map((row) => (row.id === data.id ? data : row)));
      toast({ title: t("construction.qaQc.templatePublished") });
      onChanged?.();
    } catch (cause) {
      toast({
        variant: "destructive",
        title: uiError(cause, t("construction.common.actionError")),
      });
    }
  };

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="font-semibold">{t("construction.qaQc.templates")}</h3>
        <Button onClick={openCreate}>
          <FilePlus2 className="mr-2 h-4 w-4" />
          {t("construction.qaQc.newTemplate")}
        </Button>
      </div>
      {templates.length === 0 ? (
        <PageEmpty
          message={t("construction.qaQc.noTemplates")}
          action={
            <Button onClick={openCreate}>
              <FilePlus2 className="mr-2 h-4 w-4" />
              {t("construction.qaQc.newTemplate")}
            </Button>
          }
        />
      ) : (
        <div className="grid gap-3 lg:grid-cols-2">
          {templates.map((template) => (
            <article
              key={template.id}
              className="space-y-3 rounded-xl border bg-card p-4 shadow-sm"
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-semibold">{template.code}</p>
                  <p className="text-sm text-muted-foreground">{template.nameVi}</p>
                </div>
                <Badge variant="outline" className="shrink-0 whitespace-nowrap">
                  {t(
                    template.isPublished
                      ? "construction.qaQc.published"
                      : "construction.qaQc.draft",
                  )}
                </Badge>
              </div>
              <p className="text-sm">v{template.version}</p>
              {!template.isPublished && (
                <Button size="sm" onClick={() => void publishTemplate(template)}>
                  <ClipboardCheck className="mr-2 h-4 w-4" />
                  {t("construction.qaQc.publish")}
                </Button>
              )}
            </article>
          ))}
        </div>
      )}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[90vh] overflow-x-hidden overflow-y-auto sm:max-w-2xl">
          <form onSubmit={createTemplate} className="min-w-0 space-y-4">
            <DialogHeader>
              <DialogTitle>{t("construction.qaQc.newTemplate")}</DialogTitle>
            </DialogHeader>
            <DialogDescription>{t("construction.qaQc.templateFormIntro")}</DialogDescription>
            <div className="grid gap-4 sm:grid-cols-2">
              {(
                [
                  ["code", "construction.qaQc.templateCode"],
                  ["nameVi", "construction.qaQc.nameVi"],
                  ["name", "construction.qaQc.nameEn"],
                  ["nameZh", "construction.qaQc.nameZh"],
                  ["nameJa", "construction.qaQc.nameJa"],
                ] as const
              ).map(([field, labelKey]) => (
                <div key={field} className="space-y-1.5">
                  <Label htmlFor={`qa-template-${field}`}>{t(labelKey)}</Label>
                  <Input
                    id={`qa-template-${field}`}
                    required
                    maxLength={field === "code" ? 60 : 200}
                    value={form[field]}
                    onChange={(event) => setForm({ ...form, [field]: event.target.value })}
                  />
                </div>
              ))}
            </div>
            <fieldset className="min-w-0 max-w-full space-y-3 rounded-lg border p-4">
              <legend className="px-1 text-sm font-semibold">
                {t("construction.qaQc.checklistDefinition")}
              </legend>
              <p className="text-sm text-muted-foreground">
                {t("construction.qaQc.checklistDefinitionHint")}
              </p>
              <p className="text-xs text-muted-foreground sm:hidden">
                {t("construction.qaQc.scrollTableHint")}
              </p>
              <div className="min-w-0 max-w-full overflow-x-auto">
                <table className="w-full min-w-[560px] text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="pb-2 pr-3 font-medium">
                        {t("construction.qaQc.inspectionItem")}
                      </th>
                      <th className="pb-2 pr-3 font-medium">
                        {t("construction.qaQc.acceptanceCriteria")}
                      </th>
                      <th className="w-11 pb-2">
                        <span className="sr-only">{t("common.actions")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {checklistItems.map((item, index) => (
                      <tr key={`${item.code}-${index}`} className="border-b last:border-0">
                        <td className="py-3 pr-3 align-top">
                          <Label htmlFor={`qa-item-${index}`} className="sr-only">
                            {t("construction.qaQc.inspectionItem")}
                          </Label>
                          <Input
                            id={`qa-item-${index}`}
                            required
                            maxLength={300}
                            placeholder={t("construction.qaQc.inspectionItemExample")}
                            value={item.title}
                            onChange={(event) =>
                              updateChecklistItem(index, { title: event.target.value })
                            }
                          />
                        </td>
                        <td className="py-3 pr-3 align-top">
                          <Label htmlFor={`qa-criteria-${index}`} className="sr-only">
                            {t("construction.qaQc.acceptanceCriteria")}
                          </Label>
                          <Input
                            id={`qa-criteria-${index}`}
                            required
                            maxLength={1000}
                            placeholder={t("construction.qaQc.acceptanceCriteriaExample")}
                            value={item.acceptanceCriteria}
                            onChange={(event) =>
                              updateChecklistItem(index, {
                                acceptanceCriteria: event.target.value,
                              })
                            }
                          />
                        </td>
                        <td className="py-3 align-top">
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            disabled={checklistItems.length === 1}
                            aria-label={t("construction.qaQc.removeInspectionItem")}
                            onClick={() => {
                              const next = checklistItems.filter(
                                (_, itemIndex) => itemIndex !== index,
                              );
                              setChecklistItems(next);
                              setAdvancedJson(serializeQaQcTemplateSchema(next));
                            }}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  const next = [...checklistItems, nextChecklistItem(checklistItems)];
                  setChecklistItems(next);
                  setAdvancedJson(serializeQaQcTemplateSchema(next));
                }}
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("construction.qaQc.addInspectionItem")}
              </Button>
            </fieldset>
            <details className="rounded-lg border bg-muted/30 px-4 py-3">
              <summary className="cursor-pointer text-sm font-medium">
                {t("construction.qaQc.advancedJson")}
              </summary>
              <div className="mt-3 space-y-3">
                <p className="text-xs text-muted-foreground">
                  {t("construction.qaQc.advancedJsonHint")}
                </p>
                <Textarea
                  aria-label={t("a11yUi.checklistSchema")}
                  maxLength={20000}
                  className="min-h-36 font-mono text-xs"
                  value={advancedJson}
                  onChange={(event) => setAdvancedJson(event.target.value)}
                />
                <Button type="button" size="sm" variant="outline" onClick={applyAdvancedJson}>
                  {t("construction.qaQc.applyJson")}
                </Button>
              </div>
            </details>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={acting}>
                {t("common.save")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </section>
  );
}
