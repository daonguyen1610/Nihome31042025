import { useEffect } from "react";
import type { KeyboardEvent, MouseEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Bell, CheckCheck, Loader2, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  fetchNotifications,
  fetchUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
  removeNotification,
} from "@/store/notificationSlice";
import { useAppDispatch, useAppSelector } from "@/store";
import type { NotificationDto } from "@/services/notificationApi";
import { formatRelativeTime, moduleIcon } from "@/components/layout/notificationUtils";

const PAGE_SIZE = 20;

const Notifications = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { items, unreadCount, loading, error, loadedCount, hasMore } = useAppSelector(
    (state) => state.notifications,
  );

  useEffect(() => {
    void dispatch(fetchNotifications({ skip: 0, take: PAGE_SIZE }));
    void dispatch(fetchUnreadCount());
  }, [dispatch]);

  const handleItemClick = (notification: NotificationDto) => {
    if (!notification.isRead) {
      void dispatch(markNotificationRead(notification.id));
    }

    if (notification.linkUrl) {
      navigate(notification.linkUrl);
    }
  };

  const handleItemKeyDown = (event: KeyboardEvent<HTMLDivElement>, notification: NotificationDto) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    handleItemClick(notification);
  };

  const handleDelete = (event: MouseEvent<HTMLButtonElement>, id: number) => {
    event.stopPropagation();
    void dispatch(removeNotification(id));
  };

  const handleLoadMore = () => {
    void dispatch(fetchNotifications({ skip: loadedCount, take: PAGE_SIZE }));
  };

  const handleRefresh = () => {
    void dispatch(fetchNotifications({ skip: 0, take: PAGE_SIZE }));
    void dispatch(fetchUnreadCount());
  };

  return (
    <AdminLayout>
      <div className="space-y-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-muted-foreground">
              {t("notify.title")}
            </p>
            <h1 className="mt-2 text-2xl font-bold tracking-tight text-foreground md:text-3xl">
              {t("notify.pageTitle")}
            </h1>
            <p className="mt-2 text-sm text-muted-foreground">
              {t("notify.unreadCount").replace("{count}", unreadCount.toString())}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" onClick={handleRefresh} disabled={loading}>
              {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Bell className="h-4 w-4" />}
              {t("notify.refresh")}
            </Button>
            <Button
              type="button"
              onClick={() => void dispatch(markAllNotificationsRead())}
              disabled={unreadCount === 0}
            >
              <CheckCheck className="h-4 w-4" />
              {t("notify.markAllRead")}
            </Button>
          </div>
        </div>

        <section className="overflow-hidden rounded-lg border bg-white">
          {loading && items.length === 0 ? (
            <div className="flex items-center justify-center gap-2 px-4 py-20 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              {t("notify.loading")}
            </div>
          ) : error && items.length === 0 ? (
            <div className="flex flex-col items-center justify-center gap-3 px-4 py-20 text-center">
              <p className="text-sm font-semibold text-destructive">{t("notify.error")}</p>
              <Button type="button" variant="outline" onClick={handleRefresh}>
                {t("common.retry")}
              </Button>
            </div>
          ) : items.length === 0 ? (
            <div className="px-4 py-20 text-center text-sm text-muted-foreground">{t("notify.empty")}</div>
          ) : (
            <div className="divide-y">
              {items.map((notification) => {
                const Icon = moduleIcon(notification);

                return (
                  <div
                    key={notification.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => handleItemClick(notification)}
                    onKeyDown={(event) => handleItemKeyDown(event, notification)}
                    className={cn(
                      "group grid cursor-pointer grid-cols-[2.25rem_minmax(0,1fr)_auto] gap-3 px-4 py-4 text-left outline-none transition hover:bg-muted/60 focus-visible:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring md:px-5",
                      !notification.isRead && "bg-primary/5",
                    )}
                  >
                    <span className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-full bg-muted text-primary">
                      <Icon className="h-4 w-4" />
                    </span>
                    <span className="min-w-0">
                      <span className="flex items-start gap-2">
                        <span className="min-w-0 flex-1 text-sm font-bold leading-snug text-foreground">
                          {notification.title}
                        </span>
                        {!notification.isRead ? (
                          <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-sky-500" />
                        ) : null}
                      </span>
                      {notification.body ? (
                        <span className="mt-1 block text-sm leading-relaxed text-muted-foreground">
                          {notification.body}
                        </span>
                      ) : null}
                      <span className="mt-2 block text-xs text-muted-foreground">
                        {formatRelativeTime(notification.createdAt)}
                      </span>
                    </span>
                    <button
                      type="button"
                      aria-label={t("notify.delete")}
                      onClick={(event) => handleDelete(event, notification.id)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter" || event.key === " ") {
                          event.stopPropagation();
                        }
                      }}
                      className="mt-0.5 rounded-md p-2 text-muted-foreground transition hover:bg-destructive/10 hover:text-destructive focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:opacity-0 md:group-hover:opacity-100 md:focus-visible:opacity-100"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </section>

        {items.length > 0 && hasMore ? (
          <div className="flex justify-center">
            <Button type="button" variant="outline" onClick={handleLoadMore} disabled={loading}>
              {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {t("notify.loadMore")}
            </Button>
          </div>
        ) : null}
      </div>
    </AdminLayout>
  );
};

export default Notifications;
