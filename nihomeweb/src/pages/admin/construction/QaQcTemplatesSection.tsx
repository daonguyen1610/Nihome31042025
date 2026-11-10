import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
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
import { usePermissions } from "@/hooks/usePermissions";
import { useUiError } from "@/hooks/useUiError";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
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
  const { has } = usePermissions();
  const canManageTemplates = has(ADMIN_PERMS.constructionQaQcTemplateManage);
  const canViewTranslations = has(ADMIN_PERMS.translations);
  const [templates, setTemplates] = useState<QaQcTemplateResponse[]>([]);
  const [open, setOpen] = useState(false);
  const [acting, setActing] = useState(false);
  const [form, setForm] = useState({ code: "", nameVi: "" });
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
      const nameVi = form.nameVi.trim();
      const { data } = await adminApi.createQaQcTemplate(projectId, {
        code: form.code.trim(),
        nameVi,
        // Vietnamese is the source language; other languages are managed in
        // Content translations, so start every language field from the source.
        name: nameVi,
        nameZh: nameVi,
        nameJa: nameVi,
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
    setForm({ code: "", nameVi: "" });
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
      <Dialog open={open} onOpenChange={(nextOpen) => !acting && setOpen(nextOpen)}>
        <DialogContent
          className="max-h-[calc(100dvh-2rem)] overflow-hidden p-0 sm:max-w-3xl"
          onEscapeKeyDown={(event) => acting && event.preventDefault()}
          onInteractOutside={(event) => acting && event.preventDefault()}
        >
          <form
            onSubmit={createTemplate}
            className="flex max-h-[calc(100dvh-2rem)] min-h-0 min-w-0 flex-col"
            aria-busy={acting}
          >
            <div className="min-h-0 space-y-5 overflow-y-auto px-4 pb-6 pt-5 sm:px-6 sm:pt-6">
              <DialogHeader className="pr-8 text-left">
                <DialogTitle>{t("construction.qaQc.newTemplate")}</DialogTitle>
                <DialogDescription>{t("construction.qaQc.templateFormIntro")}</DialogDescription>
              </DialogHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label htmlFor="qa-template-code">{t("construction.qaQc.templateCode")}</Label>
                  <Input
                    id="qa-template-code"
                    required
                    maxLength={60}
                    value={form.code}
                    onChange={(event) => setForm({ ...form, code: event.target.value })}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="qa-template-nameVi">{t("construction.qaQc.nameVi")}</Label>
                  <Input
                    id="qa-template-nameVi"
                    required
                    maxLength={200}
                    value={form.nameVi}
                    onChange={(event) => setForm({ ...form, nameVi: event.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">
                    {t("construction.qaQc.nameSourceHint")}
                  </p>
                  {canViewTranslations && (
                    <Button asChild type="button" variant="link" className="h-auto p-0 text-sm">
                      <Link to="/admin/translations?tab=entity&type=QaQcTemplate">
                        {t("cat.openTranslations")}
                      </Link>
                    </Button>
                  )}
                </div>
              </div>
              <fieldset className="min-w-0 max-w-full rounded-lg border">
                <legend className="ml-3 px-1 text-sm font-semibold">
                  {t("construction.qaQc.checklistDefinition")}
                </legend>
                <div className="space-y-4 p-4 pt-2">
                  <p className="text-sm text-muted-foreground">
                    {t("construction.qaQc.checklistDefinitionHint")}
                  </p>
                  <ol className="divide-y rounded-md border">
                    {checklistItems.map((item, index) => (
                      <li key={`${item.code}-${index}`} className="space-y-3 p-3 sm:p-4">
                        <div className="flex items-center justify-between gap-3">
                          <p className="text-sm font-medium">
                            {t("construction.qaQc.inspectionItem")} {index + 1}
                          </p>
                          <Button
                            type="button"
                            size="icon"
                            variant="ghost"
                            className="h-11 w-11 shrink-0 text-muted-foreground"
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
                        </div>
                        <div className="grid gap-3 sm:grid-cols-2">
                          <div className="space-y-1.5">
                            <Label htmlFor={`qa-item-${index}`}>
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
                          </div>
                          <div className="space-y-1.5">
                            <Label htmlFor={`qa-criteria-${index}`}>
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
                          </div>
                        </div>
                      </li>
                    ))}
                  </ol>
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
                </div>
              </fieldset>
              {canManageTemplates && (
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
                      spellCheck={false}
                      className="min-h-36 font-mono text-xs"
                      value={advancedJson}
                      onChange={(event) => setAdvancedJson(event.target.value)}
                    />
                    <Button type="button" size="sm" variant="outline" onClick={applyAdvancedJson}>
                      {t("construction.qaQc.applyJson")}
                    </Button>
                  </div>
                </details>
              )}
            </div>
            <DialogFooter className="shrink-0 gap-2 border-t bg-background px-4 py-3 sm:px-6">
              <Button
                type="button"
                variant="outline"
                disabled={acting}
                onClick={() => setOpen(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={acting}>
                {t("construction.qaQc.saveTemplate")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </section>
  );
}
