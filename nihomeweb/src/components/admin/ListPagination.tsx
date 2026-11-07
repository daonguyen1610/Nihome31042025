import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { useEffect } from "react";

export function ListPagination({
  page,
  pageSize,
  total,
  onPageChange,
  disabled = false,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
  disabled?: boolean;
}) {
  const { t } = useI18n();
  const lastPage = Math.max(1, Math.ceil(total / pageSize));
  useEffect(() => {
    if (!disabled && page > lastPage) onPageChange(lastPage);
  }, [disabled, page, lastPage, onPageChange]);
  return (
    <div
      data-testid="list-pagination"
      className="flex flex-wrap items-center justify-between gap-3 text-sm text-muted-foreground"
    >
      <p role="status">{t("common.pagination.total").replace("{total}", String(total))}</p>
      <div className="flex items-center gap-2">
        <Button
          data-testid="pagination-previous"
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled || page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          {t("common.pagination.prev")}
        </Button>
        <span className="tabular-nums">
          {page} / {Math.max(1, Math.ceil(total / pageSize))}
        </span>
        <Button
          data-testid="pagination-next"
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled || page * pageSize >= total}
          onClick={() => onPageChange(page + 1)}
        >
          {t("common.pagination.next")}
        </Button>
      </div>
    </div>
  );
}
