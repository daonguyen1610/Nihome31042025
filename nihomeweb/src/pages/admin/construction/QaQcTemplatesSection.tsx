import { useEffect, useState, type FormEvent } from "react";
import { ClipboardCheck, FilePlus2 } from "lucide-react";
import { PageEmpty } from "@/components/PageState";
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
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { useI18n } from "@/lib/i18n";
import { adminApi, type QaQcTemplateResponse } from "@/services/adminApi";

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
    schemaJson: "{}",
  });

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
    try {
      JSON.parse(form.schemaJson);
    } catch {
      toast({ variant: "destructive", title: t("construction.qaQc.invalidSchemaJson") });
      return;
    }
    setActing(true);
    try {
      const { data } = await adminApi.createQaQcTemplate(projectId, form);
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
        <Button onClick={() => setOpen(true)}>
          <FilePlus2 className="mr-2 h-4 w-4" />
          {t("construction.qaQc.newTemplate")}
        </Button>
      </div>
      {templates.length === 0 ? (
        <PageEmpty
          message={t("construction.qaQc.noTemplates")}
          action={
            <Button onClick={() => setOpen(true)}>
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
                <Badge variant="outline">
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
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <form onSubmit={createTemplate} className="space-y-4">
            <DialogHeader>
              <DialogTitle>{t("construction.qaQc.newTemplate")}</DialogTitle>
            </DialogHeader>
            <div className="grid gap-3 sm:grid-cols-2">
              <Input
                aria-label={t("construction.qaQc.templateCode")}
                required
                maxLength={60}
                placeholder={t("construction.qaQc.templateCode")}
                value={form.code}
                onChange={(event) => setForm({ ...form, code: event.target.value })}
              />
              <Input
                aria-label={t("construction.qaQc.nameVi")}
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameVi")}
                value={form.nameVi}
                onChange={(event) => setForm({ ...form, nameVi: event.target.value })}
              />
              <Input
                aria-label={t("construction.qaQc.nameEn")}
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameEn")}
                value={form.name}
                onChange={(event) => setForm({ ...form, name: event.target.value })}
              />
              <Input
                aria-label={t("construction.qaQc.nameZh")}
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameZh")}
                value={form.nameZh}
                onChange={(event) => setForm({ ...form, nameZh: event.target.value })}
              />
              <Input
                aria-label={t("construction.qaQc.nameJa")}
                required
                maxLength={200}
                placeholder={t("construction.qaQc.nameJa")}
                value={form.nameJa}
                onChange={(event) => setForm({ ...form, nameJa: event.target.value })}
              />
              <Textarea
                aria-label={t("a11yUi.checklistSchema")}
                required
                maxLength={20000}
                className="min-h-36 font-mono text-xs sm:col-span-2"
                value={form.schemaJson}
                onChange={(event) => setForm({ ...form, schemaJson: event.target.value })}
              />
            </div>
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
