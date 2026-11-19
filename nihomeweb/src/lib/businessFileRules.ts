/**
 * Business documents uploaded to design records, tenders, contracts and capability
 * documents. Mirrors BusinessFileRules on the server so a wrong file is caught
 * before it uploads; each module's size limit comes from Settings.
 * Free of imports so it can be unit-tested.
 */
export const BUSINESS_FILE_EXTENSIONS = [
  ".pdf",
  ".doc",
  ".docx",
  ".xls",
  ".xlsx",
  ".png",
  ".jpg",
  ".jpeg",
] as const;
export const BUSINESS_FILE_ACCEPT = BUSINESS_FILE_EXTENSIONS.join(",");

export type BusinessFileProblem =
  | { code: "unsupported"; extension: string }
  | { code: "empty" }
  | { code: "tooLarge"; sizeMb: number };

/** `maxMb` null skips the size check (the limit could not be read; the server still checks). */
export function businessFileProblem(
  file: { name: string; size: number },
  maxMb: number | null,
): BusinessFileProblem | null {
  const dot = file.name.lastIndexOf(".");
  const extension = dot > 0 ? file.name.slice(dot).toLowerCase() : "";
  if (!(BUSINESS_FILE_EXTENSIONS as readonly string[]).includes(extension))
    return { code: "unsupported", extension };
  if (file.size === 0) return { code: "empty" };
  const sizeMb = file.size / 1024 / 1024;
  if (maxMb != null && sizeMb > maxMb)
    return { code: "tooLarge", sizeMb: Math.round(sizeMb * 10) / 10 };
  return null;
}
