import api from "@/lib/api";

export interface NotificationDto {
  id: number;
  module: "JobApplication" | "Contact" | "System" | string;
  title: string;
  body?: string | null;
  linkUrl?: string | null;
  isRead: boolean;
  createdAt: string;
}

export const notificationApi = {
  getAll: (skip = 0, take = 20) =>
    api.get<NotificationDto[]>(`/notifications?skip=${skip}&take=${take}`),

  getUnreadCount: () =>
    api.get<{ count: number }>("/notifications/unread-count"),

  markRead: (id: number) =>
    api.patch<NotificationDto>(`/notifications/${id}/mark-read`),

  markAllRead: () =>
    api.post<{ count: number }>("/notifications/mark-all-read"),

  delete: (id: number) =>
    api.delete(`/notifications/${id}`),
};
