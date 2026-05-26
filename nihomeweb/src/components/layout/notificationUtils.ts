import { Briefcase, Mail, Wrench } from "lucide-react";
import type { NotificationDto } from "@/services/notificationApi";

export function resolveCurrentLocale() {
  if (typeof document !== "undefined" && document.documentElement.lang) {
    return document.documentElement.lang;
  }

  if (typeof navigator !== "undefined" && navigator.language) {
    return navigator.language;
  }

  return "vi-VN";
}

export function formatRelativeTime(value: string) {
  const date = new Date(value);
  const timestamp = date.getTime();

  if (Number.isNaN(timestamp)) {
    return "";
  }

  const locale = resolveCurrentLocale();
  const diffSeconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));
  const relativeTimeFormatter = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });

  if (diffSeconds < 60) return relativeTimeFormatter.format(0, "second");
  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return relativeTimeFormatter.format(-diffMinutes, "minute");
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return relativeTimeFormatter.format(-diffHours, "hour");
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return relativeTimeFormatter.format(-diffDays, "day");

  return new Intl.DateTimeFormat(locale, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

export function moduleIcon(notification: NotificationDto) {
  if (notification.module === "JobApplication") return Briefcase;
  if (notification.module === "Contact") return Mail;
  return Wrench;
}
