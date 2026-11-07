import { useCallback, useEffect, useRef } from "react";

/** Ignore obsolete responses without coupling page state to the HTTP client. */
export function useRequestGuard() {
  const sequence = useRef(0);
  useEffect(
    () => () => {
      sequence.current++;
    },
    [],
  );
  return useCallback(() => {
    const request = ++sequence.current;
    return () => request === sequence.current;
  }, []);
}
