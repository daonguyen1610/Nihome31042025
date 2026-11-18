import { useCallback, useState } from "react";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useUiError } from "@/hooks/useUiError";
import { collectLookupPages } from "@/lib/pagination";
import { createCsvFilename, downloadCsv, type CsvColumn } from "@/lib/exportCsv";

type ListPage<T> = {
  items: T[];
  total?: number;
  totalCount?: number;
  page: number;
  pageSize: number;
};

/**
 * Exports every row of a paginated list, with the filters currently applied,
 * to a CSV file that Excel opens with Vietnamese text intact.
 */
export function useListExport() {
  const { t } = useI18n();
  const { toast } = useToast();
  const uiError = useUiError();
  const [exporting, setExporting] = useState(false);

  const exportList = useCallback(
    async <T extends { id: number }>(options: {
      /** File name prefix, e.g. "customers"; the date is appended. */
      filename: string;
      /** Loads one page with the list's current filters. */
      loadPage: (page: number) => Promise<{ data: ListPage<T> }>;
      columns: CsvColumn<T>[];
    }) => {
      setExporting(true);
      try {
        const { data } = await collectLookupPages(options.loadPage);
        downloadCsv({
          filename: createCsvFilename(options.filename),
          columns: options.columns,
          rows: data.items,
        });
      } catch (error) {
        toast({
          variant: "destructive",
          title: t("common.exportFailed"),
          description: uiError(error),
        });
      } finally {
        setExporting(false);
      }
    },
    [t, toast, uiError],
  );

  return { exporting, exportList };
}
