/**
 * System-wide display settings: the time zone every date/time is rendered in,
 * the currency every money value uses, and the language used for locale
 * formatting. Values are loaded once from the public
 * `GET /site-settings/display` endpoint and default to
 * Asia/Ho_Chi_Minh + VND when the API is unavailable.
 *
 * Formatting helpers read the current values at call time, so a settings
 * change in admin applies to every page without a reload.
 */
import { useSyncExternalStore } from "react";
import api from "@/lib/api";
import { setActiveCurrency, type CurrencyCode } from "@/lib/numberFormat";

export const DEFAULT_TIME_ZONE = "Asia/Ho_Chi_Minh";
export const DEFAULT_CURRENCY: CurrencyCode = "VND";

export type DisplaySettings = {
  timeZoneId: string;
  currency: CurrencyCode;
  language: string;
};

export const DEFAULT_DISPLAY_SETTINGS: DisplaySettings = {
  timeZoneId: DEFAULT_TIME_ZONE,
  currency: DEFAULT_CURRENCY,
  language: "vi",
};

let current: DisplaySettings = DEFAULT_DISPLAY_SETTINGS;
const listeners = new Set<() => void>();

function emit() {
  for (const listener of listeners) listener();
}

export function getDisplaySettings(): DisplaySettings {
  return current;
}

export function subscribeDisplaySettings(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Normalize a currency code coming from the API or local state. */
export function normalizeCurrency(code: string | null | undefined): CurrencyCode {
  return (code ?? "").trim().toUpperCase() === "USD" ? "USD" : "VND";
}

function isValidTimeZone(id: string): boolean {
  try {
    new Intl.DateTimeFormat("en-US", { timeZone: id });
    return true;
  } catch {
    return false;
  }
}

/** Apply time zone + currency. Unknown values fall back to the defaults. */
export function applyDisplaySettings(next: {
  timeZoneId?: string | null;
  currency?: string | null;
}): void {
  const requested = next.timeZoneId?.trim();
  const timeZoneId = requested && isValidTimeZone(requested) ? requested : DEFAULT_TIME_ZONE;
  const currency = normalizeCurrency(next.currency);
  if (current.timeZoneId === timeZoneId && current.currency === currency) return;
  current = { ...current, timeZoneId, currency };
  setActiveCurrency(currency);
  emit();
}

/** Keep locale formatting in step with the selected UI language. */
export function setDisplayLanguage(language: string): void {
  if (current.language === language) return;
  current = { ...current, language };
  emit();
}

/**
 * Load the configured display settings. The public endpoint needs no auth and
 * any failure keeps the Vietnamese defaults, so the app always renders.
 */
export async function loadDisplaySettings(): Promise<DisplaySettings> {
  try {
    const { data } = await api.get<{ timeZoneId?: string | null; currency?: string | null }>(
      "/site-settings/display",
    );
    applyDisplaySettings(data);
  } catch {
    applyDisplaySettings({ timeZoneId: DEFAULT_TIME_ZONE, currency: DEFAULT_CURRENCY });
  }
  return current;
}

export function useDisplaySettings(): DisplaySettings {
  return useSyncExternalStore(subscribeDisplaySettings, getDisplaySettings, getDisplaySettings);
}

const DATE_LOCALES: Record<string, string> = {
  vi: "vi-VN",
  en: "en-US",
  zh: "zh-CN",
  ja: "ja-JP",
};

/** Map an i18n language code to a BCP 47 locale; explicit locales pass through. */
export function localeFor(language?: string | null): string {
  const value = (language ?? current.language ?? "vi").trim();
  if (!value) return DATE_LOCALES.vi;
  const mapped = DATE_LOCALES[value.toLowerCase()];
  if (mapped) return mapped;
  return value.includes("-") ? value : DATE_LOCALES.vi;
}

export type DateInput = Date | string | number | null | undefined;

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;
const HAS_ZONE = /(?:[zZ]|[+-]\d{2}:?\d{2})$/;

function isDateOnly(value: DateInput): boolean {
  return typeof value === "string" && DATE_ONLY.test(value.trim());
}

/**
 * Parse a backend value. Date-only strings are calendar values, and bare
 * date-times are UTC (the API serializes UTC instants without an offset).
 */
function toDate(value: Exclude<DateInput, null | undefined>): Date | null {
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
  if (typeof value === "number") {
    const fromNumber = new Date(value);
    return Number.isNaN(fromNumber.getTime()) ? null : fromNumber;
  }
  const text = value.trim();
  if (!text) return null;
  const normalized = DATE_ONLY.test(text)
    ? `${text}T00:00:00Z`
    : HAS_ZONE.test(text)
      ? text
      : `${text}Z`;
  const parsed = new Date(normalized);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

const EMPTY_DATE = "—";

type DateOptions = Intl.DateTimeFormatOptions;

const FORMAT_FIELDS = [
  "weekday",
  "era",
  "year",
  "month",
  "day",
  "dayPeriod",
  "hour",
  "minute",
  "second",
  "fractionalSecondDigits",
  "timeZoneName",
];

/**
 * Callers that choose the displayed fields (`{ month: "short" }`) or a preset
 * style own the whole format; callers that only override the zone still get
 * the base fields for the helper they called.
 */
function withDefaults(
  options: DateOptions | undefined,
  defaults: DateOptions,
): Intl.DateTimeFormatOptions {
  if (!options) return defaults;
  if (options.dateStyle || options.timeStyle) return options;
  const definesFormat = FORMAT_FIELDS.some((field) => field in options);
  return definesFormat ? options : { ...defaults, ...options };
}

function formatWith(
  value: DateInput,
  language: string | null | undefined,
  options: Intl.DateTimeFormatOptions | undefined,
  defaults: Intl.DateTimeFormatOptions,
): string {
  if (value == null || value === "") return EMPTY_DATE;
  const date = toDate(value);
  if (!date) return EMPTY_DATE;
  const merged = withDefaults(options, defaults);
  const timeZone = merged.timeZone ?? (isDateOnly(value) ? "UTC" : current.timeZoneId);
  try {
    return new Intl.DateTimeFormat(localeFor(language), { ...merged, timeZone }).format(date);
  } catch {
    return new Intl.DateTimeFormat(localeFor(language), { ...merged, timeZone: "UTC" }).format(
      date,
    );
  }
}

/** Format a date (no time) in the configured time zone. */
export function formatDate(
  value: DateInput,
  language?: string | null,
  options?: Intl.DateTimeFormatOptions,
): string {
  return formatWith(value, language, options, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
  });
}

/** Format a date and time in the configured time zone. */
export function formatDateTime(
  value: DateInput,
  language?: string | null,
  options?: Intl.DateTimeFormatOptions,
): string {
  return formatWith(value, language, options, {
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

/** Format only the time of day in the configured time zone. */
export function formatTime(
  value: DateInput,
  language?: string | null,
  options?: Intl.DateTimeFormatOptions,
): string {
  return formatWith(value, language, options, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

/** Locale-aware number formatting that follows the selected language. */
export function formatNumber(
  value: number | string | null | undefined,
  language?: string | null,
  options?: Intl.NumberFormatOptions,
): string {
  if (value == null) return "—";
  const numeric = typeof value === "string" ? Number(value) : value;
  if (!Number.isFinite(numeric)) return "—";
  return new Intl.NumberFormat(localeFor(language), options).format(numeric);
}

function zonedParts(date: Date, extra: Intl.DateTimeFormatOptions) {
  try {
    return new Intl.DateTimeFormat("en-CA", {
      ...extra,
      timeZone: current.timeZoneId,
    }).formatToParts(date);
  } catch {
    // An unparseable zone must not break date input prefill/submit.
    return new Intl.DateTimeFormat("en-CA", { ...extra, timeZone: "UTC" }).formatToParts(date);
  }
}

function part(parts: Intl.DateTimeFormatPart[], type: Intl.DateTimeFormatPartTypes): string {
  return parts.find((item) => item.type === type)?.value ?? "";
}

/** Today's calendar date (YYYY-MM-DD) in the configured time zone. */
export function todayIsoDate(): string {
  const parts = zonedParts(new Date(), { year: "numeric", month: "2-digit", day: "2-digit" });
  return `${part(parts, "year")}-${part(parts, "month")}-${part(parts, "day")}`;
}

/**
 * Convert an instant to the `YYYY-MM-DDTHH:mm` value a datetime-local input
 * expects, using the configured time zone instead of the browser's.
 */
export function toDateTimeLocalValue(value: DateInput): string {
  if (value == null || value === "") return "";
  const date = toDate(value);
  if (!date) return "";
  const parts = zonedParts(date, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  });
  return `${part(parts, "year")}-${part(parts, "month")}-${part(parts, "day")}T${part(
    parts,
    "hour",
  )}:${part(parts, "minute")}`;
}

/** Offset (ms) of the configured zone from UTC at the given instant. */
function zoneOffsetMs(date: Date): number {
  const parts = zonedParts(date, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hourCycle: "h23",
  });
  const wallClock = Date.UTC(
    Number(part(parts, "year")),
    Number(part(parts, "month")) - 1,
    Number(part(parts, "day")),
    Number(part(parts, "hour")),
    Number(part(parts, "minute")),
    Number(part(parts, "second")),
  );
  return wallClock - Math.floor(date.getTime() / 1000) * 1000;
}

const LOCAL_DATE_TIME = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/;

/**
 * Convert a `datetime-local` value (wall time in the configured time zone) to
 * the UTC ISO instant the API stores. The browser's own offset never applies.
 */
export function dateTimeLocalToIso(value: string | null | undefined): string | null {
  const match = LOCAL_DATE_TIME.exec((value ?? "").trim());
  if (!match) return null;
  const wallClock = Date.UTC(
    Number(match[1]),
    Number(match[2]) - 1,
    Number(match[3]),
    Number(match[4]),
    Number(match[5]),
    Number(match[6] ?? 0),
  );
  if (Number.isNaN(wallClock)) return null;
  // Resolve the offset twice so times near a DST change use the offset in force.
  let offset = zoneOffsetMs(new Date(wallClock));
  const secondOffset = zoneOffsetMs(new Date(wallClock - offset));
  if (secondOffset !== offset) offset = secondOffset;
  return new Date(wallClock - offset).toISOString();
}
