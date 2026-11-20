import axios, { type AxiosInstance } from "axios";
import {
  stageLargeFormFiles,
  type ChunkPutResult,
  type ChunkTransport,
  type ResumeStore,
  type StagedForm,
} from "@/lib/chunkedUpload";

// The session id survives a reload so choosing the same file resumes it. Storage may be
// unavailable (private mode); then the upload simply starts over next time.
const resumeStore: ResumeStore = {
  get: (key) => {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  },
  set: (key, value) => {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* resume is a convenience only */
    }
  },
  remove: (key) => {
    try {
      localStorage.removeItem(key);
    } catch {
      /* resume is a convenience only */
    }
  },
};

const putResult = (error: unknown): ChunkPutResult => {
  if (!axios.isAxiosError(error) || axios.isCancel(error)) throw error;
  const status = error.response?.status;
  const receivedBytes = (error.response?.data as { receivedBytes?: number } | undefined)
    ?.receivedBytes;
  if (status === 409 && typeof receivedBytes === "number") return { kind: "resume", receivedBytes };
  if (status === 404) return { kind: "gone" };
  // No response (offline, dropped), timeouts, throttling and server errors are retried.
  if (status === undefined || status === 408 || status === 429 || status >= 500)
    return { kind: "retry", error };
  throw error;
};

const transport = (client: AxiosInstance, target: string, file: File): ChunkTransport => ({
  start: async () =>
    (
      await client.post<{ id: string; receivedBytes: number; chunkSize: number }>(
        "/upload-sessions",
        { targetPath: target, fileName: file.name, fileSize: file.size },
      )
    ).data,
  status: async (id) => {
    try {
      return (
        await client.get<{ receivedBytes: number; chunkSize: number }>(`/upload-sessions/${id}`)
      ).data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) return null;
      throw error;
    }
  },
  put: async (id, offset, chunk, signal) => {
    try {
      const { data } = await client.put<{ receivedBytes: number }>(
        `/upload-sessions/${id}/chunks`,
        chunk,
        { params: { offset }, headers: { "Content-Type": "application/octet-stream" }, signal },
      );
      return { kind: "stored", receivedBytes: data.receivedBytes };
    } catch (error) {
      return putResult(error);
    }
  },
});

/**
 * Sends the large files of an upload form in resumable chunks before the request at
 * `target` (an API path); see {@link stageLargeFormFiles}.
 */
export const stageUploadForm = (
  client: AxiosInstance,
  form: FormData,
  target: string,
  signal: AbortSignal,
  onProgress: (sentBytes: number) => void,
  resumeKeys: string[],
): Promise<StagedForm | null> =>
  stageLargeFormFiles(form, {
    target,
    resumeKeys,
    transportFor: (file) => transport(client, target, file),
    store: resumeStore,
    signal,
    onProgress,
  });

/** The server closed the sessions with the upload request. */
export const forgetStagedUploads = (resumeKeys: string[]) => resumeKeys.forEach(resumeStore.remove);

/** The user cancelled: drop the staged bytes on the server too. */
export function discardStagedUploads(client: AxiosInstance, resumeKeys: string[]) {
  for (const key of resumeKeys) {
    const id = resumeStore.get(key);
    resumeStore.remove(key);
    if (id) void client.delete(`/upload-sessions/${id}`).catch(() => undefined);
  }
}
