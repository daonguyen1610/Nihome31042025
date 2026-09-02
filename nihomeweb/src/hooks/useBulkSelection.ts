import { useCallback, useMemo, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

export interface BulkDeleteFailure<TId extends string | number> {
  id: TId;
  reason: unknown;
}

export interface BulkDeleteResult<TId extends string | number> {
  success: number;
  failed: number;
  failures: BulkDeleteFailure<TId>[];
}

export interface UseBulkSelectionOptions<TId extends string | number> {
  /** Ids currently visible in the table (used for select-all + indeterminate). */
  visibleIds: TId[];
  /**
   * Called for each id when the user confirms bulk delete. Any thrown error is
   * counted as a failure.
   */
  deleteOne: (id: TId) => Promise<unknown>;
  confirmMessage?: string;
  /** Called once after all deletions attempt, e.g. to reload the list. */
  onAfter?: (result: BulkDeleteResult<TId>) => void | Promise<void>;
}

/**
 * Shared bulk-select + bulk-delete state for admin list pages. Provides:
 * - `selectedIds` set + toggle helpers
 * - `allVisibleSelected` / `someVisibleSelected` for header checkbox
 * - `handleBulkDelete()` that confirms, calls `deleteOne` in parallel, and
 *   toasts the outcome using shared `common.*` i18n keys.
 */
export function useBulkSelection<TId extends string | number>({
  visibleIds,
  deleteOne,
  confirmMessage,
  onAfter,
}: UseBulkSelectionOptions<TId>) {
  const { t } = useI18n();
  const { toast } = useToast();

  const [selectedIds, setSelectedIds] = useState<Set<TId>>(new Set());
  const [bulkDeleting, setBulkDeleting] = useState(false);

  const allVisibleSelected = useMemo(
    () => visibleIds.length > 0 && visibleIds.every((id) => selectedIds.has(id)),
    [visibleIds, selectedIds],
  );
  const someVisibleSelected = useMemo(
    () => !allVisibleSelected && visibleIds.some((id) => selectedIds.has(id)),
    [visibleIds, selectedIds, allVisibleSelected],
  );

  const toggleAllVisible = useCallback(
    (checked: boolean) => {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        if (checked) visibleIds.forEach((id) => next.add(id));
        else visibleIds.forEach((id) => next.delete(id));
        return next;
      });
    },
    [visibleIds],
  );

  const toggleOne = useCallback((id: TId, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }, []);

  const clearSelection = useCallback(() => setSelectedIds(new Set()), []);

  const handleBulkDelete = useCallback(async () => {
    if (selectedIds.size === 0) return;
    const ids = Array.from(selectedIds);
    const resolvedConfirmMessage = (confirmMessage ?? t("common.confirmDeleteMany")).replace(
      "{count}",
      ids.length.toString(),
    );
    if (!window.confirm(resolvedConfirmMessage)) return;
    setBulkDeleting(true);
    try {
      const results = await Promise.allSettled(ids.map((id) => deleteOne(id)));
      const failures = results.flatMap((result, index) => result.status === "rejected"
        ? [{ id: ids[index], reason: result.reason }]
        : []);
      const failed = failures.length;
      const success = results.length - failed;
      setSelectedIds(new Set(failures.map((failure) => failure.id)));
      await onAfter?.({ success, failed, failures });
      if (failed === 0) {
        toast({
          title: t("common.bulkDeleteSuccess").replace(
            "{count}",
            success.toString(),
          ),
        });
      } else {
        toast({
          title: t("common.bulkDeletePartial")
            .replace("{success}", success.toString())
            .replace("{failed}", failed.toString()),
          variant: success === 0 ? "destructive" : undefined,
        });
      }
    } finally {
      setBulkDeleting(false);
    }
  }, [selectedIds, deleteOne, confirmMessage, onAfter, t, toast]);

  return {
    selectedIds,
    setSelectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  };
}
