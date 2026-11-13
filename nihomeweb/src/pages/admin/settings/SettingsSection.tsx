import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { settingsSectionDescriptionId, settingsSectionId } from "./settingsSectionIds";

type SettingsSectionProps = {
  sectionKey: string;
  icon: LucideIcon;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
};

/**
 * One ruled section of the general settings sheet. Sections share a single
 * frame, so the divider lives here and the first section drops it.
 */
const SettingsSection = ({
  sectionKey,
  icon: Icon,
  title,
  description,
  children,
  className,
}: SettingsSectionProps) => {
  const titleId = `${settingsSectionId(sectionKey)}-title`;
  const descriptionId = settingsSectionDescriptionId(sectionKey);

  return (
    <section
      id={settingsSectionId(sectionKey)}
      aria-labelledby={titleId}
      className={cn("scroll-mt-24 border-t px-4 py-6 first:border-t-0 sm:px-6 sm:py-8", className)}
    >
      <header className="flex items-start gap-3">
        <Icon className="mt-0.5 h-5 w-5 shrink-0 text-primary" aria-hidden="true" />
        <div className="min-w-0">
          <h2 id={titleId} className="text-base font-semibold">
            {title}
          </h2>
          {description ? (
            <p id={descriptionId} className="mt-1 max-w-prose text-sm text-muted-foreground">
              {description}
            </p>
          ) : null}
        </div>
      </header>
      <div className="mt-5">{children}</div>
    </section>
  );
};

export default SettingsSection;
