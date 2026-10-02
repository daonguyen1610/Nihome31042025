export type QuoteCatalogType = "InvestmentRate" | "Boq";

const CATALOG_PATHS: Record<QuoteCatalogType, string> = {
  InvestmentRate: "/admin/material-rates/investment",
  Boq: "/admin/material-rates/boq",
};

/**
 * Only quote screens may be returned to: the value comes from the URL, so
 * anything else (another host, a protocol-relative "//", an unrelated page) is
 * dropped instead of becoming an open redirect.
 */
export const readQuoteReturnTo = (value: string | null): string | null => {
  if (!value) return null;
  const path = value.split(/[?#]/, 1)[0];
  if (path !== "/admin/quotes" && !/^\/admin\/quotes\/\d+$/.test(path)) return null;
  return value;
};

/**
 * Where the catalog screen should send the user back to. The new-quote form
 * lives on /admin/quotes without its own URL, so create=1 is added to reopen it.
 */
export const quoteReturnTarget = (pathname: string, search: string): string => {
  if (pathname !== "/admin/quotes") return `${pathname}${search}`;
  const params = new URLSearchParams(search);
  params.set("create", "1");
  return `${pathname}?${params.toString()}`;
};

/** Link from a quote form to the catalog screen, opening the create dialog. */
export const buildCatalogSetupLink = (catalogType: QuoteCatalogType, returnTo: string): string => {
  const params = new URLSearchParams({ create: "1" });
  const safeReturnTo = readQuoteReturnTo(returnTo);
  if (safeReturnTo) params.set("returnTo", safeReturnTo);
  return `${CATALOG_PATHS[catalogType]}?${params.toString()}`;
};
