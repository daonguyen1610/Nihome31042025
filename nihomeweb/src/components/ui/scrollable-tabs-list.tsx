import * as React from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { TabsList } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

type ScrollableTabsListProps = React.ComponentPropsWithoutRef<typeof TabsList> & {
  previousLabel: string;
  nextLabel: string;
  centerActiveTab?: boolean;
};

export function ScrollableTabsList({
  children,
  className,
  previousLabel,
  nextLabel,
  centerActiveTab = false,
  ...props
}: ScrollableTabsListProps) {
  const viewportRef = React.useRef<HTMLDivElement>(null);
  const revealFrameRef = React.useRef(0);
  const [canScrollLeft, setCanScrollLeft] = React.useState(false);
  const [canScrollRight, setCanScrollRight] = React.useState(false);

  const updateScrollState = React.useCallback(() => {
    const viewport = viewportRef.current;
    if (!viewport) return;
    setCanScrollLeft(viewport.scrollLeft > 1);
    setCanScrollRight(viewport.scrollLeft + viewport.clientWidth < viewport.scrollWidth - 1);
  }, []);

  React.useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) return;
    const revealActiveTab = () => {
      const active = viewport.querySelector<HTMLElement>("[data-state='active']");
      if (active) {
        const tabBounds = active.getBoundingClientRect();
        const viewportBounds = viewport.getBoundingClientRect();
        const offset = centerActiveTab
          ? tabBounds.left + tabBounds.width / 2 - viewportBounds.left - viewportBounds.width / 2
          : tabBounds.left < viewportBounds.left
            ? tabBounds.left - viewportBounds.left
            : Math.max(0, tabBounds.right - viewportBounds.right);
        viewport.scrollLeft += offset;
      }
      updateScrollState();
    };
    const revealAfterLayout = () => {
      cancelAnimationFrame(revealFrameRef.current);
      revealFrameRef.current = requestAnimationFrame(() => {
        revealActiveTab();
        // Revealing the tab may add arrow controls. Let their layout settle
        // before checking the final available viewport width.
        revealFrameRef.current = requestAnimationFrame(revealActiveTab);
      });
    };
    updateScrollState();
    revealAfterLayout();
    let lastWidth = viewport.parentElement!.getBoundingClientRect().width;
    const observer = new ResizeObserver(([entry]) => {
      if (entry.contentRect.width === lastWidth) return;
      lastWidth = entry.contentRect.width;
      revealAfterLayout();
    });
    // Observe the whole control, so manual scrolling that hides an arrow
    // doesn't recenter the selected tab and undo the user's navigation.
    observer.observe(viewport.parentElement!);
    const selectionObserver = new MutationObserver(revealAfterLayout);
    selectionObserver.observe(viewport, {
      subtree: true,
      childList: true,
      characterData: true,
      attributes: true,
      attributeFilter: ["data-state"],
    });
    return () => {
      observer.disconnect();
      selectionObserver.disconnect();
      cancelAnimationFrame(revealFrameRef.current);
    };
  }, [centerActiveTab, updateScrollState]);

  const scroll = (direction: number) => {
    cancelAnimationFrame(revealFrameRef.current);
    viewportRef.current?.scrollBy({ left: direction * 240, behavior: "smooth" });
  };

  return (
    <div className="flex min-w-0 items-center gap-1">
      {canScrollLeft ? (
        <Button
          type="button"
          variant="secondary"
          size="icon"
          className="h-8 w-8 shrink-0 rounded-full shadow"
          aria-label={previousLabel}
          data-testid="scrollable-tabs-previous"
          onClick={() => scroll(-1)}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
      ) : null}
      <div
        ref={viewportRef}
        onScroll={updateScrollState}
        onWheel={() => cancelAnimationFrame(revealFrameRef.current)}
        onPointerDown={() => cancelAnimationFrame(revealFrameRef.current)}
        className="min-w-0 flex-1 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      >
        <TabsList
          {...props}
          className={cn("inline-flex h-auto w-max min-w-full justify-start", className)}
        >
          {children}
        </TabsList>
      </div>
      {canScrollRight ? (
        <Button
          type="button"
          variant="secondary"
          size="icon"
          className="h-8 w-8 shrink-0 rounded-full shadow"
          aria-label={nextLabel}
          data-testid="scrollable-tabs-next"
          onClick={() => scroll(1)}
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
      ) : null}
    </div>
  );
}
