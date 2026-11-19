/**
 * Every file upload in progress, for the upload tray. The API client feeds it
 * from axios upload progress; the tray reads it with useSyncExternalStore.
 * Free of imports so it can be unit-tested.
 */
export type UploadStatus = "uploading" | "done" | "failed" | "cancelled";

export interface UploadItem {
  id: number;
  name: string;
  /** Total bytes to send. */
  size: number;
  loaded: number;
  status: UploadStatus;
  /** The request error, translated by the tray. */
  error?: unknown;
}

export function createUploadTracker() {
  let items: UploadItem[] = [];
  let nextId = 1;
  const aborts = new Map<number, () => void>();
  const listeners = new Set<() => void>();

  const update = (id: number, change: Partial<UploadItem>) => {
    items = items.map((item) => (item.id === id ? { ...item, ...change } : item));
    listeners.forEach((listener) => listener());
  };
  const isUploading = (id: number) =>
    items.some((item) => item.id === id && item.status === "uploading");

  return {
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    getSnapshot: () => items,
    activeCount: () => items.filter((item) => item.status === "uploading").length,

    start({ name, size, abort }: { name: string; size: number; abort: () => void }) {
      const id = nextId++;
      aborts.set(id, abort);
      items = [...items, { id, name, size, loaded: 0, status: "uploading" }];
      listeners.forEach((listener) => listener());
      return id;
    },
    progress(id: number, loaded: number) {
      if (isUploading(id)) update(id, { loaded });
    },
    finish(id: number) {
      aborts.delete(id);
      const item = items.find((entry) => entry.id === id);
      if (item?.status === "uploading") update(id, { status: "done", loaded: item.size });
    },
    fail(id: number, error: unknown) {
      aborts.delete(id);
      // A cancelled upload rejects too; it stays cancelled, not failed.
      if (isUploading(id)) update(id, { status: "failed", error });
    },
    cancel(id: number) {
      if (!isUploading(id)) return;
      update(id, { status: "cancelled" });
      aborts.get(id)?.();
      aborts.delete(id);
    },
    clearFinished() {
      items = items.filter((item) => item.status === "uploading");
      listeners.forEach((listener) => listener());
    },
  };
}

export type UploadTracker = ReturnType<typeof createUploadTracker>;

/** Marks the upload tray, which stays usable above an open dialog or sheet. */
export const UPLOAD_TRAY_ATTRIBUTE = "data-upload-tray";

/**
 * True for a click in the upload tray. Dialogs and sheets do not treat it as a
 * click outside, so cancelling an upload started from one does not close it.
 */
export const isInUploadTray = (target: EventTarget | null) =>
  typeof Element !== "undefined" &&
  target instanceof Element &&
  target.closest(`[${UPLOAD_TRAY_ATTRIBUTE}]`) !== null;

/** The one tracker the app uses. */
export const uploadTracker = createUploadTracker();
