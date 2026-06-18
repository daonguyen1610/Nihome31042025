import api, { withIdempotencyKey } from "@/lib/api";

export interface MeResponse {
  id: number;
  phoneNumber: string;
  fullName?: string;
  email?: string;
  role: string;
  isActive: boolean;
  avatarUrl?: string;
}

export interface UserDocumentResponse {
  id: number;
  documentType: string;
  originalName: string;
  fileUrl: string;
  contentType: string;
  size: number;
  createdAt: string;
}

export const meApi = {
  getMe: () => api.get<MeResponse>("/users/me"),

  updateMe: (data: { fullName?: string; email?: string }, idempotencyKey?: string) =>
    api.put<MeResponse>("/users/me", data, withIdempotencyKey(idempotencyKey)),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    api.post<{ message: string }>("/users/me/change-password", data),

  listDocuments: () =>
    api.get<UserDocumentResponse[]>("/users/me/documents"),

  uploadDocument: (file: File, documentType: string) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("documentType", documentType);
    return api.post<UserDocumentResponse>("/users/me/documents", formData);
  },

  deleteDocument: (id: number) =>
    api.delete<void>(`/users/me/documents/${id}`),
};
