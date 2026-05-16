import { configureStore } from "@reduxjs/toolkit";
import { beforeEach, describe, expect, it, vi } from "vitest";
import notificationReducer, {
  fetchNotifications,
  fetchUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
  removeNotification,
} from "@/store/notificationSlice";
import { notificationApi, type NotificationDto } from "@/services/notificationApi";

vi.mock("@/services/notificationApi", () => ({
  notificationApi: {
    getAll: vi.fn(),
    getUnreadCount: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
    delete: vi.fn(),
  },
}));

const unread: NotificationDto = {
  id: 1,
  module: "Contact",
  title: "New contact",
  body: "Need support",
  linkUrl: "/admin/contacts",
  isRead: false,
  createdAt: "2026-05-16T00:00:00Z",
};

const read: NotificationDto = {
  id: 2,
  module: "System",
  title: "System",
  isRead: true,
  createdAt: "2026-05-16T00:00:00Z",
};

function createTestStore() {
  return configureStore({ reducer: { notifications: notificationReducer } });
}

describe("notificationSlice", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetchNotifications stores items without changing unread count", async () => {
    vi.mocked(notificationApi.getAll).mockResolvedValueOnce({ data: [unread, read] } as never);
    const store = createTestStore();

    await store.dispatch(fetchNotifications());

    expect(store.getState().notifications.items).toEqual([unread, read]);
    expect(store.getState().notifications.unreadCount).toBe(0);
  });

  it("fetchNotifications stores error on failure", async () => {
    vi.mocked(notificationApi.getAll).mockRejectedValueOnce(new Error("fail"));
    const store = createTestStore();

    await store.dispatch(fetchNotifications());

    expect(store.getState().notifications.loading).toBe(false);
    expect(store.getState().notifications.error).toBe("An unexpected error occurred");
  });

  it("fetchUnreadCount stores backend count", async () => {
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValueOnce({ data: { count: 5 } } as never);
    const store = createTestStore();

    await store.dispatch(fetchUnreadCount());

    expect(store.getState().notifications.unreadCount).toBe(5);
  });

  it("markNotificationRead optimistically marks one notification read", async () => {
    vi.mocked(notificationApi.getAll).mockResolvedValueOnce({ data: [unread] } as never);
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValueOnce({ data: { count: 1 } } as never);
    vi.mocked(notificationApi.markRead).mockResolvedValueOnce({ data: { ...unread, isRead: true } } as never);
    const store = createTestStore();
    await store.dispatch(fetchNotifications());
    await store.dispatch(fetchUnreadCount());

    const promise = store.dispatch(markNotificationRead(unread.id));
    expect(store.getState().notifications.items[0].isRead).toBe(true);
    expect(store.getState().notifications.unreadCount).toBe(0);
    await promise;
  });

  it("markAllNotificationsRead optimistically marks all notifications read", async () => {
    vi.mocked(notificationApi.getAll).mockResolvedValueOnce({ data: [unread, { ...unread, id: 3 }] } as never);
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValueOnce({ data: { count: 2 } } as never);
    vi.mocked(notificationApi.markAllRead).mockResolvedValueOnce({ data: { count: 2 } } as never);
    const store = createTestStore();
    await store.dispatch(fetchNotifications());
    await store.dispatch(fetchUnreadCount());

    const promise = store.dispatch(markAllNotificationsRead());
    expect(store.getState().notifications.unreadCount).toBe(0);
    expect(store.getState().notifications.items.every((item) => item.isRead)).toBe(true);
    await promise;
  });

  it("removeNotification optimistically removes an unread notification", async () => {
    vi.mocked(notificationApi.getAll).mockResolvedValueOnce({ data: [unread, read] } as never);
    vi.mocked(notificationApi.getUnreadCount).mockResolvedValueOnce({ data: { count: 1 } } as never);
    vi.mocked(notificationApi.delete).mockResolvedValueOnce({ data: {} } as never);
    const store = createTestStore();
    await store.dispatch(fetchNotifications());
    await store.dispatch(fetchUnreadCount());

    const promise = store.dispatch(removeNotification(unread.id));
    expect(store.getState().notifications.items).toEqual([read]);
    expect(store.getState().notifications.unreadCount).toBe(0);
    await promise;
  });
});
