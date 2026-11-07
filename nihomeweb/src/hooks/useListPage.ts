import { useCallback, useState } from "react";

/** Reset during render so a changed filter never requests the old page. */
export function useListPage(filterKey: string) {
  const [state, setState] = useState({ filterKey, page: 1 });
  if (state.filterKey !== filterKey) setState({ filterKey, page: 1 });
  const page = state.filterKey === filterKey ? state.page : 1;
  const setPage = useCallback(
    (next: number) => setState({ filterKey, page: Math.max(1, next) }),
    [filterKey],
  );
  return [page, setPage] as const;
}
