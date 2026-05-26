import { useEffect, useMemo, useRef, useState } from "react";
import type { KeyboardEvent, MouseEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  Bell,
  CheckCheck,
  Loader2,
  Trash2,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import {
  fetchNotifications,
  fetchUnreadCount,
  markAllNotificationsRead,
  markNotificationRead,
  removeNotification,
  resetNotifications,
} from "@/store/notificationSlice";
import { useAppDispatch, useAppSelector } from "@/store";
import type { NotificationDto } from "@/services/notificationApi";
import { formatRelativeTime, moduleIcon } from "@/components/layout/notificationUtils";

const POLL_INTERVAL_MS = 60_000;

export function NotificationBell() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const countRequestRef = useRef(false);
  const accessToken = useAppSelector((state) => state.auth.accessToken);
  const { items, unreadCount, loading, error } = useAppSelector((state) => state.notifications);

  useEffect(() => {
    if (!accessToken) {
      dispatch(resetNotifications());
      return;
    }

    const refreshUnreadCount = () => {
      if (countRequestRef.current || document.hidden) return;
      countRequestRef.current = true;
      void dispatch(fetchUnreadCount()).finally(() => {
        countRequestRef.current = false;
      });
    };

    refreshUnreadCount();

    const interval = window.setInterval(refreshUnreadCount, POLL_INTERVAL_MS);

    return () => window.clearInterval(interval);
  }, [accessToken, dispatch]);

  useEffect(() => {
    if (open && accessToken) {
      void dispatch(fetchNotifications({ skip: 0, take: 20 }));
    }
  }, [accessToken, dispatch, open]);

  const unreadLabel = useMemo(() => {
    if (unreadCount > 99) return "99+";
    return unreadCount.toString();
  }, [unreadCount]);

  const handleItemClick = (notification: NotificationDto) => {
    if (!notification.isRead) {
      void dispatch(markNotificationRead(notification.id));
    }

    if (notification.linkUrl) {
      setOpen(false);
      navigate(notification.linkUrl);
    }
  };

  const handleViewAll = () => {
    setOpen(false);
    navigate("/admin/notifications");
  };

  const handleDelete = (event: MouseEvent<HTMLButtonElement>, id: number) => {
    event.stopPropagation();
    void dispatch(removeNotification(id));
  };

  const handleItemKeyDown = (event: KeyboardEvent<HTMLDivElement>, notification: NotificationDto) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    handleItemClick(notification);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          className="relative w-9 h-9 rounded-full flex items-center justify-center hover:bg-muted transition"
          style={{ color: "hsl(var(--admin-sidebar-text))" }}
          aria-label={t("notify.open")}
          type="button"
        >
          <Bell className="w-4 h-4" />
          {unreadCount > 0 ? (
            <Badge className="absolute -right-1 -top-1 min-w-5 h-5 px-1.5 justify-center border-white bg-red-600 text-[10px] leading-none">
              {unreadLabel}
            </Badge>
          ) : null}
        </button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(22rem,calc(100vw-2rem))] p-0 overflow-hidden">
        <div className="flex items-center justify-between gap-3 border-b px-4 py-3">
          <div>
            <h2 className="text-sm font-bold">{t("notify.title")}</h2>
            <p className="text-xs text-muted-foreground">{t("notify.unreadCount").replace("{count}", unreadCount.toString())}</p>
          </div>
          <button
            type="button"
            onClick={() => void dispatch(markAllNotificationsRead())}
            disabled={unreadCount === 0}
            className="inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-medium text-primary transition hover:bg-primary/10 disabled:pointer-events-none disabled:opacity-50"
          >
            <CheckCheck className="w-3.5 h-3.5" />
            {t("notify.markAllRead")}
          </button>
        </div>
        <div className="border-b px-4 py-2">
          <button
            type="button"
            onClick={handleViewAll}
            className="text-xs font-semibold text-primary transition hover:underline"
          >
            {t("notify.viewAll")}
          </button>
        </div>

        <div className="max-h-[26rem] overflow-y-auto">
          {loading && items.length === 0 ? (
            <div className="flex items-center justify-center gap-2 px-4 py-8 text-sm text-muted-foreground">
              <Loader2 className="w-4 h-4 animate-spin" />
              {t("notify.loading")}
            </div>
          ) : error && items.length === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-destructive">{t("notify.error")}</div>
          ) : items.length === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-muted-foreground">{t("notify.empty")}</div>
          ) : (
            items.map((notification) => {
              const Icon = moduleIcon(notification);

              return (
                <div
                  key={notification.id}
                  role="button"
                  tabIndex={0}
                  onClick={() => handleItemClick(notification)}
                  onKeyDown={(event) => handleItemKeyDown(event, notification)}
                  className={cn(
                    "group flex w-full cursor-pointer items-start gap-3 border-b px-4 py-3 text-left outline-none transition last:border-b-0 hover:bg-muted/60 focus-visible:bg-muted/60 focus-visible:ring-2 focus-visible:ring-ring",
                    !notification.isRead && "bg-primary/5",
                  )}
                >
                  <span className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted text-primary">
                    <Icon className="w-4 h-4" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-start gap-2">
                      <span className="min-w-0 flex-1 text-sm font-semibold leading-snug text-foreground">
                        {notification.title}
                      </span>
                      {!notification.isRead ? <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-sky-500" /> : null}
                    </span>
                    {notification.body ? (
                      <span className="mt-1 block line-clamp-2 text-xs leading-relaxed text-muted-foreground">
                        {notification.body}
                      </span>
                    ) : null}
                    <span className="mt-1.5 block text-[11px] text-muted-foreground">
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
                    className="rounded-md p-1.5 text-muted-foreground opacity-0 transition hover:bg-destructive/10 hover:text-destructive group-hover:opacity-100 focus-visible:opacity-100"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              );
            })
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
