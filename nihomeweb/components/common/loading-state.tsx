type LoadingStateProps = {
  label?: string;
};

export function LoadingState({
  label = "More modules will arrive in later phases.",
}: LoadingStateProps) {
  return (
    <div
      aria-live="polite"
      className="surface-card flex items-center gap-4 px-5 py-4 text-sm text-[#52606d]"
      role="status"
    >
      <div className="flex gap-2">
        <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-[#9c6b46]" />
        <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-[#d3b08a]" />
        <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-[#f3c892]" />
      </div>
      <span>{label}</span>
    </div>
  );
}
