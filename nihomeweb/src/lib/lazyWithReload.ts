import { lazy, type ComponentType } from "react";
import { isStaleBundleError, reloadOnceForNewVersion } from "@/lib/newVersionReload";

/**
 * React.lazy that, when the page's bundle file was removed by a deploy, reloads
 * once to fetch the new version instead of failing.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function lazyWithReload<T extends ComponentType<any>>(load: () => Promise<{ default: T }>) {
  return lazy(async () => {
    try {
      return await load();
    } catch (error) {
      // While the page reloads, keep showing the loading state.
      if (isStaleBundleError(error) && reloadOnceForNewVersion())
        return new Promise<never>(() => {});
      throw error;
    }
  });
}
