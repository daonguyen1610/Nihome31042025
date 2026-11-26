import { useRef, useState, type MutableRefObject } from "react";

type AutoFocusHandler = (event: Event) => void;

/** Records the focused element during the first render, before any autoFocus moves it. */
export function RememberOpener({ store }: { store: MutableRefObject<Element | null> }) {
  useState(() => {
    store.current = document.activeElement;
  });
  return null;
}

/**
 * Dialogs opened from state have no Radix trigger, so focus would land on <body>
 * after closing. Hand it back to whatever opened them.
 */
export function useReturnFocus(onCloseAutoFocus?: AutoFocusHandler) {
  const opener = useRef<Element | null>(null);
  return {
    opener,
    onCloseAutoFocus: (event: Event) => {
      onCloseAutoFocus?.(event);
      const target = opener.current;
      if (event.defaultPrevented || !(target instanceof HTMLElement)) return;
      if (!target.isConnected || target === document.body) return;
      event.preventDefault();
      target.focus();
    },
  };
}
