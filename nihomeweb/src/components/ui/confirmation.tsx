import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useLocation } from "react-router-dom";
import { useI18n } from "@/lib/i18n";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

type ConfirmationOptions = { destructive?: boolean; onConfirm?: () => void };
type Confirm = (message: string, options?: ConfirmationOptions) => Promise<boolean>;
type PendingConfirmation = {
  message: string;
  options: ConfirmationOptions;
  resolve: (value: boolean) => void;
  trigger: HTMLElement | null;
};
const ConfirmationContext = createContext<Confirm | null>(null);

export function ConfirmationProvider({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const location = useLocation();
  const [pending, setPending] = useState<PendingConfirmation | null>(null);
  const pendingRef = useRef<PendingConfirmation | null>(null);
  const triggerRef = useRef<HTMLElement | null>(null);
  const finish = useCallback((confirmed: boolean) => {
    const current = pendingRef.current;
    if (!current) return;
    pendingRef.current = null;
    setPending(null);
    if (confirmed) current.options.onConfirm?.();
    current.resolve(confirmed);
  }, []);
  const confirm = useCallback<Confirm>((message, options = {}) => {
    // Keep a single confirmation active; concurrent callers must not overwrite it.
    if (pendingRef.current) return Promise.resolve(false);
    return new Promise<boolean>((resolve) => {
      const trigger = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const next = { message, options, resolve, trigger };
      pendingRef.current = next;
      triggerRef.current = trigger;
      setPending(next);
    });
  }, []);
  useEffect(
    () => () => {
      pendingRef.current?.resolve(false);
      pendingRef.current = null;
    },
    [],
  );
  useEffect(() => {
    finish(false);
  }, [location.key, finish]);
  return (
    <ConfirmationContext.Provider value={confirm}>
      {children}
      <AlertDialog
        open={pending !== null}
        onOpenChange={(open) => {
          if (!open) finish(false);
        }}
      >
        <AlertDialogContent
          className="max-h-[90dvh] overflow-y-auto"
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            if (triggerRef.current?.isConnected) triggerRef.current.focus();
          }}
        >
          <AlertDialogHeader>
            <AlertDialogTitle>{t("common.confirmAction")}</AlertDialogTitle>
            <AlertDialogDescription className="whitespace-pre-wrap break-words">
              {pending?.message}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => finish(false)}>
              {t("common.cancel")}
            </AlertDialogCancel>
            <AlertDialogAction
              className={
                pending?.options.destructive === false
                  ? undefined
                  : "bg-destructive text-destructive-foreground hover:bg-destructive/90"
              }
              onClick={(event) => {
                event.preventDefault();
                finish(true);
              }}
            >
              {t(pending?.options.destructive === false ? "common.confirmAction" : "common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </ConfirmationContext.Provider>
  );
}

export function useConfirm(): Confirm {
  const confirm = useContext(ConfirmationContext);
  if (!confirm) throw new Error("useConfirm requires ConfirmationProvider");
  return confirm;
}
