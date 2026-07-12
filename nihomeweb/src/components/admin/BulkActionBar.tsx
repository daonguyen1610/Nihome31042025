import { Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";

interface BulkActionBarProps {
  selectedCount: number;
  bulkDeleting: boolean;
  onClear: () => void;
  onBulkDelete: () => void;
}

/**
 * Floating action bar shown above admin list tables when at least one row
 * is selected. Uses `common.*` translation keys so it's reusable across
 * every list page.
 */
export const BulkActionBar = ({
  selectedCount,
  bulkDeleting,
  onClear,
  onBulkDelete,
}: BulkActionBarProps) => {
  const { t } = useI18n();
  if (selectedCount === 0) return null;
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-muted/40 px-3 py-2 text-sm">
      <span className="font-medium">
        {t("common.selectedCount").replace("{count}", selectedCount.toString())}
      </span>
      <div className="flex gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={onClear}
          disabled={bulkDeleting}
        >
          {t("common.clearSelection")}
        </Button>
        <Button
          variant="destructive"
          size="sm"
          onClick={onBulkDelete}
          disabled={bulkDeleting}
        >
          <Trash2 className="mr-1.5 h-3.5 w-3.5" />
          {t("common.deleteSelected")}
        </Button>
      </div>
    </div>
  );
};
