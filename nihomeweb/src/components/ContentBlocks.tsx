import type { ContentItem } from "@/services/contentApi";
import { cn } from "@/lib/utils";

interface ContentBlocksProps {
  items: ContentItem[];
  className?: string;
  paragraphClassName?: string;
  imageClassName?: string;
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
        return (
          <img
            key={`${index}-${item.url}`}
            src={item.url}
            alt=""
            loading="lazy"
            className={cn("w-full rounded-2xl object-cover", imageClassName)}
          />
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
