/**
 * After a deploy, a tab opened earlier still asks for its old hashed bundle
 * files, which no longer exist. Lazy pages then never load. Reloading once
 * fetches the new index.html and its new files; a guard stops a reload loop
 * when something else is broken. Free of imports so it can be unit-tested.
 */
const RELOADED_AT = "nicon_new_version_reload_at";
const LOOP_WINDOW_MS = 60_000;
let reloadedThisPage = false;

type KeyValueStore = Pick<Storage, "getItem" | "setItem">;

/** The errors browsers raise when a dynamically imported bundle file is missing. */
export function isStaleBundleError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? "");
  return /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|is not a valid JavaScript MIME type|Unable to preload CSS/i.test(
    message,
  );
}

/**
 * Vite also reports, as a preload error, failures raised after a dynamic
 * import loaded (e.g. pdf.js rejecting a broken file). Only a missing bundle
 * file means a newer version is deployed and a reload helps.
 */
export function shouldReloadForPreloadError(payload: unknown): boolean {
  return isStaleBundleError(payload);
}

/** Reloads the page unless it already did so in the last minute. Returns whether it reloaded. */
export function reloadOnceForNewVersion(
  storage: KeyValueStore = window.sessionStorage,
  now: number = Date.now(),
  reload: () => void = () => window.location.reload(),
): boolean {
  let last: number;
  try {
    last = Number(storage.getItem(RELOADED_AT) ?? 0);
  } catch {
    // No storage (private mode): allow one reload per page load.
    if (reloadedThisPage) return false;
    reloadedThisPage = true;
    reload();
    return true;
  }
  if (now - last < LOOP_WINDOW_MS) return false;
  try {
    storage.setItem(RELOADED_AT, String(now));
  } catch {
    if (reloadedThisPage) return false;
    reloadedThisPage = true;
  }
  reload();
  return true;
}
