/**
 * Files attached to basic design documents and shop drawings. Mirrors
 * DesignFileRules on the server so a wrong file is caught before it uploads.
 * Free of imports so it can be unit-tested.
 */
export const DESIGN_FILE_EXTENSIONS = [
  ".pdf",
  ".doc",
  ".docx",
  ".xls",
  ".xlsx",
  ".png",
  ".jpg",
  ".jpeg",
] as const;
export const DESIGN_FILE_ACCEPT = DESIGN_FILE_EXTENSIONS.join(",");
export const DESIGN_FILE_MAX_MB = 20;

export type DesignFileProblem =
  | { code: "unsupported"; extension: string }
  | { code: "empty" }
  | { code: "tooLarge"; sizeMb: number };

export function designFileProblem(file: { name: string; size: number }): DesignFileProblem | null {
  const dot = file.name.lastIndexOf(".");
  const extension = dot > 0 ? file.name.slice(dot).toLowerCase() : "";
  if (!(DESIGN_FILE_EXTENSIONS as readonly string[]).includes(extension))
    return { code: "unsupported", extension };
  if (file.size === 0) return { code: "empty" };
  const sizeMb = file.size / 1024 / 1024;
  if (sizeMb > DESIGN_FILE_MAX_MB)
    return { code: "tooLarge", sizeMb: Math.round(sizeMb * 10) / 10 };
  return null;
}
