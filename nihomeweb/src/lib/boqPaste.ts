import type { QuoteItemInput } from "@/services/adminApi";
import { MAX_QUOTE_MONEY, MAX_QUOTE_QUANTITY } from "@/lib/quoteTotals";

export interface BoqPasteResult {
  items: QuoteItemInput[];
  invalidRows: number[];
}

export function normalizeBoqSortOrder(items: QuoteItemInput[]): QuoteItemInput[] {
  return items.map((item, index) => ({ ...item, sortOrder: index + 1 }));
}

export function parseBoqPaste(text: string): BoqPasteResult {
  const items: QuoteItemInput[] = [];
  const invalidRows: number[] = [];
  const lines = text.split(/\r?\n/);

  for (let index = 0; index < lines.length; index++) {
    const raw = lines[index];
    if (!raw.trim()) continue;
    const cells = raw.split("\t").map((cell) => cell.trim());
    if (isHeaderRow(cells)) continue;

    const parsed = parseRow(cells, items.length + 1);
    if (parsed) items.push(parsed);
    else invalidRows.push(index + 1);
  }

  return { items, invalidRows };
}

function parseRow(cells: string[], sortOrder: number): QuoteItemInput | null {
  if (cells.length !== 4 && cells.length !== 5) return null;

  let code: string | null = null;
  let name: string;
  let unit: string;
  let quantityText: string;
  let unitPriceText: string;

  if (cells.length === 4) {
    [name, unit, quantityText, unitPriceText] = cells;
  } else if (isNumberLike(cells[2]) && isNumberLike(cells[3])) {
    [name, unit, quantityText, unitPriceText] = cells;
    code = cells[4] || null;
  } else {
    [code, name, unit, quantityText, unitPriceText] = cells;
    code ||= null;
  }

  const quantity = parseSpreadsheetNumber(quantityText, true);
  const unitPrice = parseSpreadsheetNumber(unitPriceText);
  if (!name || !unit || name.length > 300 || unit.length > 30 || (code?.length ?? 0) > 60 ||
      !Number.isFinite(quantity) || quantity <= 0 || quantity > MAX_QUOTE_QUANTITY ||
      !Number.isFinite(unitPrice) || unitPrice < 0 || unitPrice > MAX_QUOTE_MONEY) {
    return null;
  }

  return { itemCode: code, name, unit, quantity, unitPrice, sortOrder };
}

export function parseSpreadsheetNumber(value: string, rejectAmbiguousSingleSeparator = false): number {
  const compact = value.trim().replace(/\s/g, "").replace(/[₫đ]/gi, "");
  if (!/^-?[\d.,]+$/.test(compact)) return Number.NaN;

  const sign = compact.startsWith("-") ? "-" : "";
  const unsigned = compact.replace("-", "");
  const lastDot = unsigned.lastIndexOf(".");
  const lastComma = unsigned.lastIndexOf(",");
  let normalized: string;

  if (lastDot >= 0 && lastComma >= 0) {
    const decimalSeparator = lastDot > lastComma ? "." : ",";
    const thousandsSeparator = decimalSeparator === "." ? "," : ".";
    const decimalParts = unsigned.split(decimalSeparator);
    if (decimalParts.length !== 2 || !decimalParts[0] || !decimalParts[1]) return Number.NaN;
    const integerGroups = decimalParts[0].split(thousandsSeparator);
    if (!integerGroups[0] || integerGroups[0].length > 3 ||
      integerGroups.slice(1).some((group) => group.length !== 3)) {
      return Number.NaN;
    }
    normalized = `${integerGroups.join("")}.${decimalParts[1]}`;
  } else {
    const separator = lastDot >= 0 ? "." : lastComma >= 0 ? "," : null;
    if (!separator) {
      normalized = unsigned;
    } else {
      const groups = unsigned.split(separator);
      if (!groups[0] || groups.slice(1).some((group) => !group)) return Number.NaN;
      if (groups.length > 2) {
        if (groups[0].length > 3 || !groups.slice(1).every((group) => group.length === 3)) {
          return Number.NaN;
        }
        normalized = groups.join("");
      } else if (groups[1].length === 3) {
        if (groups[0].length > 3 || rejectAmbiguousSingleSeparator) return Number.NaN;
        normalized = groups.join("");
      } else {
        normalized = `${groups[0]}.${groups[1]}`;
      }
    }
  }

  const parsed = Number(`${sign}${normalized}`);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

function isNumberLike(value: string): boolean {
  return Number.isFinite(parseSpreadsheetNumber(value));
}

function isHeaderRow(cells: string[]): boolean {
  if (cells.length < 4) return false;
  const normalized = cells.map((cell) => cell.toLocaleLowerCase("vi").replace(/[\s_/²()]/g, ""));
  return normalized.some((cell) => ["mã", "code", "itemcode"].includes(cell)) &&
    normalized.some((cell) => ["hạngmục", "tênhạngmục", "item", "itemname", "name"].includes(cell)) ||
    normalized.some((cell) => ["khốilượng", "quantity", "qty"].includes(cell)) &&
    normalized.some((cell) => ["đơngiá", "unitprice", "price"].includes(cell));
}
