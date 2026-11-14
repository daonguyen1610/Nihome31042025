import { extractApiError } from "@/lib/apiError";

export interface BoqWorkbookIssue {
  code: string;
  messageKey: string;
  sheet?: string | null;
  cell?: string | null;
  limit?: string | null;
}

export type BoqWorkbookProblem =
  { message: string } | { count: number; issues: BoqWorkbookIssue[] };

interface ErrorBody {
  messageKey?: string;
  limit?: string | null;
  issueCount?: number;
  issues?: BoqWorkbookIssue[] | null;
}

type Translate = (key: string, params?: Record<string, string | number>) => string;

// Download failures arrive as a Blob because those requests use responseType "blob".
export async function boqWorkbookProblem(
  error: unknown,
  t: Translate,
): Promise<BoqWorkbookProblem> {
  let body = (error as { response?: { data?: unknown } }).response?.data;
  if (body instanceof Blob) {
    try {
      body = JSON.parse(await body.text());
    } catch {
      body = undefined;
    }
  }
  const data = body as ErrorBody | undefined;
  if (data?.issues?.length)
    return { count: data.issueCount ?? data.issues.length, issues: data.issues };
  if (data?.messageKey) return { message: t(data.messageKey, { limit: data.limit ?? "" }) };
  return { message: extractApiError(error) };
}

export function saveBlob(blob: Blob, filename: string) {
  const href = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = filename;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
}
