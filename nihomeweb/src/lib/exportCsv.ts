export type CsvValue = string | number | boolean | Date | null | undefined | string[];

export type CsvColumn<T> = {
  header: string;
  value: keyof T | ((row: T) => CsvValue);
};

export type CsvExportOptions<T> = {
  filename: string;
  columns: CsvColumn<T>[];
  rows: T[];
};

export const CSV_UTF8_BOM = "\ufeff";

const EXCEL_DANGEROUS_PREFIX = /^[=+\-@]/;

function normalizeCsvValue(value: CsvValue): string {
  if (value === null || value === undefined) return "";
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? "" : value.toISOString();
  if (Array.isArray(value)) return value.filter(Boolean).join("; ");
  if (typeof value === "boolean") return value ? "Yes" : "No";

  return String(value);
}

function escapeCsvCell(value: CsvValue): string {
  let text = normalizeCsvValue(value).replace(/\r\n/g, "\n").replace(/\r/g, "\n");

  if (EXCEL_DANGEROUS_PREFIX.test(text)) {
    text = `'${text}`;
  }

  const escaped = text.replace(/"/g, '""');
  return /[",\n]/.test(escaped) ? `"${escaped}"` : escaped;
}

export function buildExcelCsv<T>({ columns, rows }: Omit<CsvExportOptions<T>, "filename">) {
  const headerRow = columns.map((column) => escapeCsvCell(column.header)).join(",");
  const dataRows = rows.map((row) =>
    columns
      .map((column) => {
        const raw = typeof column.value === "function" ? column.value(row) : (row[column.value] as CsvValue);
        return escapeCsvCell(raw);
      })
      .join(","),
  );

  return `${CSV_UTF8_BOM}${[headerRow, ...dataRows].join("\r\n")}`;
}

export function createCsvFilename(prefix: string, date = new Date()) {
  const safePrefix = prefix
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)+/g, "");
  const stamp = date.toISOString().slice(0, 10);

  return `${safePrefix || "export"}-${stamp}.csv`;
}

export function downloadCsv<T>(options: CsvExportOptions<T>) {
  const csv = buildExcelCsv(options);
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = options.filename;
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}
