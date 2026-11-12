import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { FieldScope } from "@/components/ui/field-scope";
import { Label } from "@/components/ui/label";
import { SearchableSelect } from "@/components/ui/searchable-select";
import { useI18n } from "@/lib/i18n";
import type { ProjectScheduleItemResponse } from "@/services/adminApi";

interface ProjectWbsFieldProps {
  projectId: number;
  items: ProjectScheduleItemResponse[];
  value: string;
  onChange: (value: string) => void;
  loading: boolean;
  failed: boolean;
  onRetry: () => void;
  legacyWbsCode?: string | null;
  disabled?: boolean;
}

export function ProjectWbsField({
  projectId,
  items,
  value,
  onChange,
  loading,
  failed,
  onRetry,
  legacyWbsCode,
  disabled = false,
}: ProjectWbsFieldProps) {
  const { t } = useI18n();
  const hasLegacyValue = !value && Boolean(legacyWbsCode?.trim());
  const options = items.map((item) => ({
    value: item.nodeId,
    label: `${item.wbsCode} · ${item.title}`,
    hint: `${item.plannedStart.slice(0, 10)} – ${item.plannedEnd.slice(0, 10)}`,
    keywords: `${item.wbsCode} ${item.title}`,
  }));

  return (
    <FieldScope className="space-y-1.5 sm:col-span-2">
      <Label htmlFor="project-wbs-node">{t("construction.common.wbs")}</Label>
      <SearchableSelect
        id="project-wbs-node"
        value={value}
        onChange={onChange}
        options={options}
        placeholder={t("construction.common.chooseWbs")}
        searchPlaceholder={t("construction.common.searchWbs")}
        emptyText={t("common.noResults")}
        loading={loading}
        loadingText={t("common.loading")}
        required={!hasLegacyValue}
        disabled={disabled || loading || failed || items.length === 0}
        data-testid="project-wbs-select"
      />
      {hasLegacyValue ? (
        <p className="text-xs text-warning-strong">
          {t("construction.common.wbsLegacyUnlinked", { code: legacyWbsCode })}
        </p>
      ) : (
        <p className="text-xs text-muted-foreground">{t("construction.common.wbsSelectHelp")}</p>
      )}
      {failed ? (
        <div className="flex flex-wrap items-center gap-2 text-sm text-destructive">
          <span>{t("construction.common.wbsLoadFailed")}</span>
          <Button type="button" variant="ghost" size="sm" onClick={onRetry}>
            {t("common.retry")}
          </Button>
        </div>
      ) : !loading && items.length === 0 && !hasLegacyValue ? (
        <p className="text-sm text-warning-strong">
          {t("construction.common.wbsNoPublished")}{" "}
          <Link
            to={`/admin/operational-projects/${projectId}?tab=schedule#project-master-schedule`}
            className="font-medium underline underline-offset-4"
          >
            {t("construction.common.openMasterSchedule")}
          </Link>
        </p>
      ) : null}
    </FieldScope>
  );
}
