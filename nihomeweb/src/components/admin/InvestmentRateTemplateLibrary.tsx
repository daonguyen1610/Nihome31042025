import { useEffect, useState } from "react";
import { Download, FileSpreadsheet } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { saveBlob } from "@/lib/boqWorkbook";
import { useI18n } from "@/lib/i18n";
import { adminApi, type InvestmentRateTemplate } from "@/services/adminApi";

/** The investment-rate Excel forms by building type, downloaded here and uploaded in a work package. */
export function InvestmentRateTemplateLibrary() {
  const { t } = useI18n();
  const { toast } = useToast();
  const [templates, setTemplates] = useState<InvestmentRateTemplate[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    adminApi
      .listInvestmentRateTemplates()
      .then(({ data }) => setTemplates(data))
      .catch((reason) => setError(extractApiError(reason)));
  }, []);

  const download = async (template: InvestmentRateTemplate) => {
    try {
      const { data } = await adminApi.downloadInvestmentRateTemplate(template.code);
      saveBlob(data, template.file);
    } catch (reason) {
      toast({ variant: "destructive", title: extractApiError(reason) });
    }
  };

  return (
    <section
      className="overflow-hidden rounded-xl border bg-card shadow-sm"
      data-testid="investment-rate-templates"
    >
      <div className="border-b px-4 py-3 sm:px-5">
        <h2 className="font-semibold">{t("investmentRate.templates.title")}</h2>
        <p className="text-sm text-muted-foreground">{t("investmentRate.templates.help")}</p>
      </div>
      {error ? (
        <p role="alert" className="p-4 text-sm text-destructive">
          {error}
        </p>
      ) : (
        <ul className="divide-y">
          {templates.map((template) => (
            <li
              key={template.code}
              className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5"
            >
              <div className="flex min-w-0 items-center gap-2">
                <FileSpreadsheet className="h-4 w-4 shrink-0 text-emerald-600" />
                <span className="break-words text-sm font-medium">{t(template.nameKey)}</span>
              </div>
              <Button
                size="sm"
                variant="outline"
                className="self-start sm:self-auto"
                data-testid={`investment-rate-template-${template.code}`}
                onClick={() => void download(template)}
              >
                <Download className="mr-1.5 h-4 w-4" />
                {t("investmentRate.dialog.downloadTemplate")}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
