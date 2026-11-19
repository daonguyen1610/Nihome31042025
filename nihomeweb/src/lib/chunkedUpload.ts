/**
 * Sends a large file in chunks so a dropped connection continues from the last
 * byte the server received instead of starting over. The upload shows in the
 * upload tray with its progress and cancel. Free of runtime imports so it can be
 * unit-tested; the API client supplies the transport.
 */
import type { UploadTracker } from "./uploadTracker";

export interface ChunkTransport<T> {
  start(signal: AbortSignal): Promise<{ uploadId: string; chunkSize: number }>;
  /** Sends bytes starting at `offset`; resolves to the bytes the server now holds. */
  send(
    uploadId: string,
    offset: number,
    chunk: Blob,
    onProgress: (sent: number) => void,
    signal: AbortSignal,
  ): Promise<number>;
  complete(uploadId: string, signal: AbortSignal): Promise<T>;
  cancel(uploadId: string): Promise<unknown>;
}

export interface ChunkedUploadOptions {
  /** True when the request failed because the connection dropped. */
  isConnectionLost(error: unknown): boolean;
  /** Bytes the server holds when it refused a chunk for starting elsewhere, else null. */
  receivedBytesOf(error: unknown): number | null;
  /** Waits before a retry; rejects when the signal aborts. */
  wait(ms: number, signal: AbortSignal): Promise<void>;
}

/** Pauses between retries after the connection drops; the last one repeats. */
export const RETRY_DELAYS_MS = [1_000, 2_000, 5_000, 10_000, 15_000];

const cancelled = () => Object.assign(new Error("canceled"), { code: "ERR_CANCELED" });

export async function uploadInChunks<T>(
  file: Blob & { name: string },
  tracker: UploadTracker,
  transport: ChunkTransport<T>,
  options: ChunkedUploadOptions,
): Promise<T> {
  const controller = new AbortController();
  const { signal } = controller;
  const id = tracker.start({ name: file.name, size: file.size, abort: () => controller.abort() });
  let uploadId: string | undefined;
  try {
    const started = await transport.start(signal);
    uploadId = started.uploadId;
    let offset = 0;
    let retries = 0;
    while (offset < file.size) {
      const from = offset;
      try {
        const chunk = file.slice(from, Math.min(file.size, from + started.chunkSize));
        offset = await transport.send(
          uploadId,
          from,
          chunk,
          (sent) => tracker.progress(id, from + sent),
          signal,
        );
        tracker.progress(id, offset);
        retries = 0;
      } catch (error) {
        if (signal.aborted) throw cancelled();
        // The server kept part of a chunk before the drop: continue from its count.
        const received = options.receivedBytesOf(error);
        if (received !== null) {
          offset = received;
          tracker.progress(id, offset);
          continue;
        }
        if (!options.isConnectionLost(error)) throw error;
        tracker.reconnecting(id);
        await options.wait(RETRY_DELAYS_MS[Math.min(retries, RETRY_DELAYS_MS.length - 1)], signal);
        retries++;
      }
    }
    const result = await transport.complete(uploadId, signal);
    tracker.finish(id);
    return result;
  } catch (error) {
    if (signal.aborted) {
      if (uploadId) void transport.cancel(uploadId).catch(() => undefined);
      throw cancelled();
    }
    tracker.fail(id, error);
    throw error;
  }
}
