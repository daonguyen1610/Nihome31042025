import { ReactNode } from "react";
import { cn } from "@/lib/utils";

interface PageHeaderProps {
  eyebrow?: string;
  title: string;
  description?: string;
  children?: ReactNode;
  className?: string;
}

const PageHeader = ({ eyebrow, title, description, children, className }: PageHeaderProps) => (
  <section className={cn("pt-32 lg:pt-40 pb-16 lg:pb-20 bg-surface", className)}>
    <div className="container-custom">
      <div className="max-w-4xl fade-in-up">
        {eyebrow && (
          <p className="text-xs uppercase tracking-[0.25em] text-primary font-semibold mb-6 font-sans">
            {eyebrow}
          </p>
        )}
        <h1 className="font-display text-5xl md:text-6xl lg:text-7xl font-bold leading-[1.05] tracking-tight text-balance">
          {title}
        </h1>
        {description && (
          <p className="mt-8 text-lg lg:text-xl text-muted-foreground leading-relaxed max-w-2xl">
            {description}
          </p>
        )}
        {children}
      </div>
    </div>
  </section>
);

export default PageHeader;
