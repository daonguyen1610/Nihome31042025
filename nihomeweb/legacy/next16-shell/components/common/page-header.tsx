import type { PageHeaderProps } from "@/types/common";

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
}: PageHeaderProps) {
  return (
    <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
      <div className="space-y-3">
        {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
        <div className="space-y-3">
          <h1 className="font-display text-4xl leading-tight text-[#1f2933] sm:text-5xl">
            {title}
          </h1>
          {description ? (
            <p className="max-w-3xl text-base leading-8 text-[#52606d] sm:text-lg">
              {description}
            </p>
          ) : null}
        </div>
      </div>
      {actions ? <div className="flex flex-wrap gap-3">{actions}</div> : null}
    </div>
  );
}
