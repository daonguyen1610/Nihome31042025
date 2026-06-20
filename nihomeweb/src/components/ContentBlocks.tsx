import type { ContentItem } from "@/services/contentApi";
import { cn } from "@/lib/utils";

interface ContentBlocksProps {
  items: ContentItem[];
  className?: string;
  paragraphClassName?: string;
  imageClassName?: string;
}

function extractYoutubeId(url: string): string | null {
  try {
    const u = new URL(url);
    if (u.hostname === "youtu.be") return u.pathname.slice(1);
    if (u.hostname.includes("youtube.com")) {
      return u.searchParams.get("v") ?? u.pathname.replace("/embed/", "") ?? null;
    }
  } catch {
    // invalid URL
  }
  return null;
}

const ContentBlocks = ({
  items,
  className,
  paragraphClassName,
  imageClassName,
}: ContentBlocksProps) => (
  <div className={cn("space-y-6", className)}>
    {items.map((item, index) => {
      if (typeof item === "string") {
        return item.trim() ? (
          <p key={`${index}-${item.slice(0, 24)}`} className={cn("whitespace-pre-line", paragraphClassName)}>
            {item}
          </p>
        ) : (
          <div key={`spacer-${index}`} className="h-4" aria-hidden="true" />
        );
      }

      if (item.type === "image") {
        const imgEl = (
          <img
            src={item.url}
            alt={item.caption ?? ""}
            loading="lazy"
            className={cn("w-full rounded-2xl object-cover", imageClassName)}
          />
        );
        return item.caption ? (
          <figure key={`${index}-${item.url}`} className="space-y-2">
            {imgEl}
            <figcaption className="text-center text-sm text-muted-foreground italic">
              {item.caption}
            </figcaption>
          </figure>
        ) : (
          <div key={`${index}-${item.url}`}>{imgEl}</div>
        );
      }

      if (item.type === "youtube") {
        const videoId = extractYoutubeId(item.url);
        if (!videoId) return null;
        return (
          <div key={`${index}-yt-${videoId}`} className="relative w-full aspect-video rounded-2xl overflow-hidden bg-muted">
            <iframe
              src={`https://www.youtube.com/embed/${videoId}`}
              title="YouTube video"
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
              allowFullScreen
              className="absolute inset-0 w-full h-full"
            />
          </div>
        );
      }

      return item.value.trim() ? (
        <p key={`${index}-${item.value.slice(0, 24)}`} className={cn("whitespace-pre-line", paragraphClassName)}>
          {item.value}
        </p>
      ) : (
        <div key={`spacer-${index}`} className="h-4" aria-hidden="true" />
      );
    })}
  </div>
);

export default ContentBlocks;
