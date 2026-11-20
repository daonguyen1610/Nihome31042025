/**
 * Resumable chunked upload, independent of the HTTP client so it can be unit-tested.
 * A dropped connection retries the same chunk; the server tells where to resume when
 * it already has more (or fewer) bytes; and a page reload continues the session saved
 * for the same file instead of starting over.
 * Free of imports so it can be unit-tested.
 */

/** Names each chunked file's session on the upload request: `field=sessionId,…`. */
export const UPLOAD_SESSIONS_HEADER = "X-Upload-Sessions";

/** Files above this go in chunks (the server's chunk size, below the IIS request limit). */
export const CHUNKED_UPLOAD_THRESHOLD_BYTES = 8 * 1024 * 1024;

export type ChunkPutResult =
  | { kind: "stored"; receivedBytes: number }
  /** The server holds a different number of bytes: continue from there. */
  | { kind: "resume"; receivedBytes: number }
  /** The session expired or was removed. */
  | { kind: "gone" }
  /** Network error or server hiccup: worth sending the same chunk again. */
  | { kind: "retry"; error: unknown };

export interface ChunkTransport {
  start(): Promise<{ id: string; receivedBytes: number; chunkSize: number }>;
  /** Null when the session is gone. */
  status(id: string): Promise<{ receivedBytes: number; chunkSize: number } | null>;
  put(id: string, offset: number, chunk: Blob, signal?: AbortSignal): Promise<ChunkPutResult>;
}

export interface ResumeStore {
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
}

export interface ChunkedUploadOptions {
  file: Blob & { name?: string; lastModified?: number };
  transport: ChunkTransport;
  store: ResumeStore;
  /** Identifies the file and its destination, e.g. contract id + purpose. */
  resumeKey: string;
  onProgress?: (sentBytes: number) => void;
  signal?: AbortSignal;
  /** Attempts per chunk after a network error before giving up. */
  retries?: number;
  wait?: (ms: number) => Promise<void>;
}

/** Why a chunked upload stopped; the UI maps the code to a translated message. */
export class ChunkedUploadError extends Error {
  constructor(
    readonly code: "UPLOAD_SESSION_GONE" | "UPLOAD_RETRIES_EXHAUSTED",
    message: string,
    readonly cause?: unknown,
  ) {
    super(message);
  }
}

// Ends early on cancel so the tray's cancel does not wait out a retry backoff.
const sleep = (ms: number, signal?: AbortSignal) =>
  new Promise<void>((resolve) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener(
      "abort",
      () => {
        clearTimeout(timer);
        resolve();
      },
      { once: true },
    );
  });

const aborted = (signal?: AbortSignal) => {
  if (signal?.aborted) throw new DOMException("The upload was cancelled.", "AbortError");
};

/** Sends every byte of the file and resolves with the session id to complete it with. */
export async function uploadInChunks({
  file,
  transport,
  store,
  resumeKey,
  onProgress,
  signal,
  retries = 5,
  wait = (ms) => sleep(ms, signal),
}: ChunkedUploadOptions): Promise<string> {
  let id = store.get(resumeKey);
  let offset = 0;
  let chunkSize = CHUNKED_UPLOAD_THRESHOLD_BYTES;
  const saved = id ? await transport.status(id) : null;
  if (id && saved) {
    offset = saved.receivedBytes;
    chunkSize = saved.chunkSize;
  } else {
    const started = await transport.start();
    id = started.id;
    offset = started.receivedBytes;
    chunkSize = started.chunkSize;
    store.set(resumeKey, id);
  }
  onProgress?.(offset);

  let attempt = 0;
  while (offset < file.size) {
    aborted(signal);
    const result = await transport.put(
      id,
      offset,
      file.slice(offset, Math.min(file.size, offset + chunkSize)),
      signal,
    );
    aborted(signal);
    if (result.kind === "stored" || result.kind === "resume") {
      offset = result.receivedBytes;
      attempt = 0;
      onProgress?.(offset);
      continue;
    }
    if (result.kind === "gone") {
      store.remove(resumeKey);
      throw new ChunkedUploadError(
        "UPLOAD_SESSION_GONE",
        "The upload session expired; choose the file again.",
      );
    }
    if (attempt >= retries) {
      throw new ChunkedUploadError(
        "UPLOAD_RETRIES_EXHAUSTED",
        "The connection kept failing while uploading.",
        result.error,
      );
    }
    await wait(Math.min(30_000, 1000 * 2 ** attempt));
    attempt += 1;
  }
  return id;
}

/** Same file, same destination: the key a reload looks up to resume. */
export const chunkedResumeKey = (
  destination: string,
  file: { name?: string; size: number; lastModified?: number },
) => `nicon:upload:${destination}:${file.name ?? ""}:${file.size}:${file.lastModified ?? 0}`;

export interface StagedForm {
  /** The form without the chunked files' bytes. */
  form: FormData;
  /** Value for {@link UPLOAD_SESSIONS_HEADER}. */
  header: string;
  stagedBytes: number;
}

/**
 * Sends every file of an upload form above {@link CHUNKED_UPLOAD_THRESHOLD_BYTES} in
 * resumable chunks for the request at `target`, and returns the form without them plus
 * the header that names their sessions. Null when no file is large enough.
 */
export async function stageLargeFormFiles(
  form: FormData,
  {
    target,
    transportFor,
    store,
    signal,
    onProgress,
    wait,
    resumeKeys,
  }: {
    target: string;
    /** Filled as sessions open, so a cancelled upload can discard them. */
    resumeKeys: string[];
    transportFor: (file: File) => ChunkTransport;
    store: ResumeStore;
    signal?: AbortSignal;
    onProgress?: (sentBytes: number) => void;
    wait?: (ms: number) => Promise<void>;
  },
): Promise<StagedForm | null> {
  const entries = [...form.entries()];
  const isLarge = (value: FormDataEntryValue): value is File =>
    typeof value !== "string" && value.size > CHUNKED_UPLOAD_THRESHOLD_BYTES;
  if (!entries.some(([, value]) => isLarge(value))) return null;

  const pairs: string[] = [];
  const used = new Set<string>();
  let staged = 0;
  const rest = new FormData();
  for (const [field, value] of entries) {
    if (!isLarge(value)) {
      rest.append(field, value);
      continue;
    }
    const resumeKey = chunkedResumeKey(`${target}|${field}`, value);
    resumeKeys.push(resumeKey);
    // The same file twice in one form needs a session of its own.
    if (used.has(store.get(resumeKey) ?? "")) store.remove(resumeKey);
    const id = await uploadInChunks({
      file: value,
      transport: transportFor(value),
      store,
      resumeKey,
      signal,
      wait,
      onProgress: (sent) => onProgress?.(staged + sent),
    });
    used.add(id);
    staged += value.size;
    pairs.push(`${encodeURIComponent(field)}=${id}`);
  }
  return { form: rest, header: pairs.join(","), stagedBytes: staged };
}

/**
 * Whether the server kept the staged files after the upload request failed with this
 * status, so sending it again does not re-upload them. A final refusal (wrong type, over
 * the module's limit) closes them. Kept in step with UploadSessionFormFilter.KeepsSessions.
 */
export const keepsStagedUploads = (status: number | undefined) =>
  status === undefined || [401, 408, 409, 429].includes(status) || status >= 500;
