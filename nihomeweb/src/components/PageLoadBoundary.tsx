import { Component, type ReactNode } from "react";
import { PageLoadFailed } from "@/components/PageState";
import { isStaleBundleError } from "@/lib/newVersionReload";

/**
 * A page that cannot load shows why and offers a reload, instead of leaving a
 * blank screen. After a deploy the automatic reload normally fetches the new
 * version first; this is what remains if that did not help.
 */
export class PageLoadBoundary extends Component<{ children: ReactNode }, { error: unknown }> {
  state: { error: unknown } = { error: null };

  static getDerivedStateFromError(error: unknown) {
    return { error };
  }

  render() {
    if (this.state.error) return <PageLoadFailed stale={isStaleBundleError(this.state.error)} />;
    return this.props.children;
  }
}
