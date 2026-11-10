export type QaQcChecklistResultValue = "Pending" | "Pass" | "Fail" | "NotApplicable";

export interface QaQcTemplateItem {
  code: string;
  title: string;
  acceptanceCriteria: string;
}

export interface QaQcChecklistResult {
  code: string;
  result: QaQcChecklistResultValue;
  note: string;
}

const resultValues = new Set<QaQcChecklistResultValue>([
  "Pending",
  "Pass",
  "Fail",
  "NotApplicable",
]);

const asRecord = (value: unknown): Record<string, unknown> | null =>
  typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;

export function parseQaQcTemplateSchema(value: string): QaQcTemplateItem[] {
  const root = asRecord(JSON.parse(value));
  if (!root || !Array.isArray(root.items)) throw new Error("invalid-template-schema");

  const seen = new Set<string>();
  return root.items.map((rawItem) => {
    const item = asRecord(rawItem);
    const code = typeof item?.code === "string" ? item.code.trim() : "";
    const title = typeof item?.title === "string" ? item.title.trim() : "";
    const acceptanceCriteria =
      typeof item?.acceptanceCriteria === "string" ? item.acceptanceCriteria.trim() : "";
    const normalizedCode = code.toLocaleUpperCase();
    if (!code || !title || seen.has(normalizedCode)) throw new Error("invalid-template-item");
    seen.add(normalizedCode);
    return { code, title, acceptanceCriteria };
  });
}

export function serializeQaQcTemplateSchema(items: QaQcTemplateItem[]): string {
  return JSON.stringify({ schemaVersion: 1, items });
}

export function buildQaQcChecklistResults(
  items: QaQcTemplateItem[],
  storedJson = "[]",
): QaQcChecklistResult[] {
  let stored: unknown = [];
  try {
    stored = JSON.parse(storedJson);
  } catch {
    stored = [];
  }
  const byCode = new Map<string, QaQcChecklistResult>();
  if (Array.isArray(stored)) {
    for (const rawResult of stored) {
      const result = asRecord(rawResult);
      const code = typeof result?.code === "string" ? result.code.trim() : "";
      const value = result?.result;
      if (
        !code ||
        typeof value !== "string" ||
        !resultValues.has(value as QaQcChecklistResultValue)
      )
        continue;
      byCode.set(code.toLocaleUpperCase(), {
        code,
        result: value as QaQcChecklistResultValue,
        note: typeof result?.note === "string" ? result.note : "",
      });
    }
  }

  return items.map((item) => {
    const storedResult = byCode.get(item.code.toLocaleUpperCase());
    return storedResult
      ? { ...storedResult, code: item.code }
      : { code: item.code, result: "Pending", note: "" };
  });
}

export function parseQaQcChecklistResults(value: string): QaQcChecklistResult[] {
  const parsed: unknown = JSON.parse(value);
  if (!Array.isArray(parsed)) throw new Error("invalid-checklist-results");
  const seen = new Set<string>();
  return parsed.map((rawResult) => {
    const item = asRecord(rawResult);
    const code = typeof item?.code === "string" ? item.code.trim() : "";
    const result = item?.result;
    const normalizedCode = code.toLocaleUpperCase();
    if (
      !code ||
      typeof result !== "string" ||
      !resultValues.has(result as QaQcChecklistResultValue) ||
      seen.has(normalizedCode)
    )
      throw new Error("invalid-checklist-result");
    seen.add(normalizedCode);
    return {
      code,
      result: result as QaQcChecklistResultValue,
      note: typeof item?.note === "string" ? item.note : "",
    };
  });
}

export function serializeQaQcChecklistResults(items: QaQcChecklistResult[]): string {
  return JSON.stringify(items);
}
