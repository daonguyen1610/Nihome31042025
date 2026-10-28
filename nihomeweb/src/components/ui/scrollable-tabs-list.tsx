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
    updateScrollState();
    const observer = new ResizeObserver(updateScrollState);
    observer.observe(viewport);
    viewport.querySelector<HTMLElement>("[data-state='active']")?.scrollIntoView({
      block: "nearest",
      inline: centerActiveTab ? "center" : "nearest",
    });
    return () => observer.disconnect();
  }, [centerActiveTab, children, updateScrollState]);

  const scroll = (direction: number) => {
    viewportRef.current?.scrollBy({ left: direction * 240, behavior: "smooth" });
  };

  return (
    <div className="relative flex min-w-0 items-center">
      {canScrollLeft ? (
        <Button
          type="button"
          variant="secondary"
          size="icon"
          className="absolute left-1 z-10 h-8 w-8 rounded-full shadow"
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
          className="absolute right-1 z-10 h-8 w-8 rounded-full shadow"
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
