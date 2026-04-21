import type { ReactNode } from "react";

type EmptyStateProps = {
  title: string;
  description: string;
  action?: ReactNode;
};

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="surface-panel px-6 py-10 text-center sm:px-10">
      <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-white text-2xl shadow-sm">
        +
      </div>
      <h2 className="mt-5 font-display text-3xl text-[#1f2933]">{title}</h2>
      <p className="mx-auto mt-4 max-w-2xl text-base leading-8 text-[#52606d]">
        {description}
      </p>
      {action ? <div className="mt-6 flex justify-center">{action}</div> : null}
    </div>
  );
}
