const API_BASE = import.meta.env.VITE_API_URL || "/api";

const getRuntimeOrigin = () =>
  typeof window !== "undefined" ? window.location.origin : undefined;

export function getApiOrigin() {
  if (!API_BASE) return "";

  try {
    return new URL(API_BASE, getRuntimeOrigin()).origin;
  } catch {
    return "";
  }
}

export function toHostRelativeUrl(value: string) {
  if (!value) return value;

  const apiOrigin = getApiOrigin();
  if (apiOrigin && value.startsWith(`${apiOrigin}/`)) {
    return value.slice(apiOrigin.length);
  }

  return value;
}

export function resolveAssetUrl(value: string) {
  const normalized = toHostRelativeUrl(value);
  if (!normalized) return normalized;

  if (
    normalized.startsWith("http://") ||
    normalized.startsWith("https://") ||
    normalized.startsWith("data:")
  ) {
    return normalized;
  }

  if (!normalized.startsWith("/")) return normalized;

  const apiOrigin = getApiOrigin();
  return apiOrigin ? `${apiOrigin}${normalized}` : normalized;
}

export function resolveSafeLinkUrl(value: string) {
  const trimmed = value.trim();
  const normalized = toHostRelativeUrl(trimmed);
  if (!normalized) return undefined;

  if (normalized.startsWith("/") && !normalized.startsWith("//") && !normalized.startsWith("/\\")) {
    return resolveAssetUrl(normalized);
  }

  try {
    const url = new URL(normalized);
    return url.protocol === "http:" || url.protocol === "https:" ? url.href : undefined;
  } catch {
    return undefined;
  }
}
