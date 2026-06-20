import type { ContentItem } from "@/services/contentApi";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";

interface ContentBlocksProps {
  items: ContentItem[];
  className?: string;
  paragraphClassName?: string;
  imageClassName?: string;
}

function extractYoutubeId(url: string): string | null {
  const match = url.match(/(?:youtu\.be\/|[?&]v=|\/embed\/|\/shorts\/|\/live\/)([A-Za-z0-9_-]{11})/);
  return match?.[1] ?? null;
}

const ContentBlocks = ({
  items,
  className,
  paragraphClassName,
  imageClassName,
}: ContentBlocksProps) => {
  const { t } = useI18n();
  return (
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
              title={t("contentBlocks.youtubeTitle")}
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
};

export default ContentBlocks;
