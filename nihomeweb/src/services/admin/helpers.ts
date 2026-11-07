import api, { withIdempotencyKey } from "@/lib/api";
import type { BusinessDocumentUploadResponse } from "./types/vendors";
export const postIdempotent = <T>(url: string, body: unknown) =>
  api.post<T>(url, body, withIdempotencyKey(crypto.randomUUID()));
export const uploadBusinessDocument = (
  area: "vendors" | "acceptance" | "as-built" | "handover",
  file: File,
) => {
  const formData = new FormData();
  formData.append("file", file);
  return api.post<BusinessDocumentUploadResponse>(`/business-documents/${area}`, formData);
};
export const managedFileName = (path: string) => {
  const normalizedPath = path.split(/[?#]/, 1)[0];
  return encodeURIComponent(decodeURIComponent(normalizedPath.split("/").pop() ?? ""));
};
