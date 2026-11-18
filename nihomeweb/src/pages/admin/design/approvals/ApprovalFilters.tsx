import type { ReactNode } from "react";
import { Search } from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { FieldScope } from "@/components/ui/field-scope";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { DesignApprovalInboxSummary } from "@/services/adminApi";
import type { BaseApprovalFilters } from "./filters";

export function ApprovalFilters({
  idPrefix,
  searchPlaceholder,
  filters,
  onChange,
  dossiers,
  hasActiveFilters,
  onReset,
  children,
}: {
  idPrefix: string;
  searchPlaceholder: string;
  filters: BaseApprovalFilters;
  onChange: (next: Partial<BaseApprovalFilters>) => void;
  dossiers: DesignApprovalInboxSummary["dossiers"];
  hasActiveFilters: boolean;
  onReset: () => void;
  children?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <section
      className="rounded-lg border border-border bg-card p-4 shadow-sm"
      aria-label={t("designApprovals.filter.title")}
    >
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <FieldScope className="md:col-span-2">
          <Label htmlFor={`${idPrefix}-search`}>{t("designApprovals.filter.search")}</Label>
          <div className="relative mt-1">
            <Search
              className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground"
              aria-hidden
            />
            <Input
              id={`${idPrefix}-search`}
              type="search"
              className="pl-8"
              value={filters.search}
              onChange={(event) => onChange({ search: event.target.value })}
              placeholder={searchPlaceholder}
            />
          </div>
        </FieldScope>
        <FieldScope>
          <Label>{t("designApprovals.filter.dossier")}</Label>
          <SearchableSelect
            ariaLabel={t("designApprovals.filter.dossier")}
            className="mt-1"
            value={filters.designProjectId != null ? String(filters.designProjectId) : ""}
            onChange={(value) => onChange({ designProjectId: value ? Number(value) : null })}
            options={[
              { value: "", label: t("designApprovals.filter.allDossiers") },
              ...dossiers.map((dossier) => ({
                value: String(dossier.id),
                label: dossier.name,
                hint: dossier.projectCode,
                keywords: dossier.projectCode,
              })),
            ]}
            placeholder={t("designApprovals.filter.allDossiers")}
          />
        </FieldScope>
        <FieldScope>
          <Label htmlFor={`${idPrefix}-sort`}>{t("designApprovals.filter.sort")}</Label>
          <Select
            value={filters.newestFirst ? "newest" : "oldest"}
            onValueChange={(value) => onChange({ newestFirst: value === "newest" })}
          >
            <SelectTrigger id={`${idPrefix}-sort`} className="mt-1 h-9">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="oldest">{t("designApprovals.sort.oldest")}</SelectItem>
              <SelectItem value="newest">{t("designApprovals.sort.newest")}</SelectItem>
            </SelectContent>
          </Select>
        </FieldScope>
        {children}
      </div>
      {hasActiveFilters && (
        <div className="mt-4">
          <Button variant="ghost" size="sm" onClick={onReset}>
            {t("designApprovals.clearFilters")}
          </Button>
        </div>
      )}
    </section>
  );
}
