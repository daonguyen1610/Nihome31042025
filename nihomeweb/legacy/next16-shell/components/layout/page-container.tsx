import type { ReactNode } from "react";

type PageContainerProps = {
  children: ReactNode;
  className?: string;
};

export function PageContainer({ children, className }: PageContainerProps) {
  const classes = ["mx-auto w-full max-w-6xl px-6 sm:px-10 lg:px-12", className]
    .filter(Boolean)
    .join(" ");

  return <div className={classes}>{children}</div>;
}
