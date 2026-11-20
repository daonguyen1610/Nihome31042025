import axios, { type AxiosResponse } from "axios";
import api from "@/lib/api";
import { uploadTracker } from "@/lib/uploadTracker";
import {
  chunkedResumeKey,
  uploadInChunks,
  type ChunkPutResult,
  type ResumeStore,
} from "@/lib/chunkedUpload";

export type ContractUploadPurpose = "Attachment" | "Appendix";

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

/**
 * Sends a large contract file in resumable chunks, then lets `complete` take it. The
 * upload tray shows its progress and can cancel it, which also discards the session.
 */
export async function uploadContractFileInChunks<T>(
  contractId: number,
  purpose: ContractUploadPurpose,
  file: File,
  complete: (sessionId: string) => Promise<AxiosResponse<T>>,
): Promise<AxiosResponse<T>> {
  const controller = new AbortController();
  const trackerId = uploadTracker.start({
    name: file.name,
    size: file.size,
    abort: () => controller.abort(),
  });
  const resumeKey = chunkedResumeKey(`contract:${contractId}:${purpose}`, file);
  let sessionId: string | undefined;
  try {
    sessionId = await uploadInChunks({
      file,
      resumeKey,
      store: resumeStore,
      signal: controller.signal,
      onProgress: (sent) => uploadTracker.progress(trackerId, sent),
      transport: {
        start: async () => {
          const { data } = await api.post<{ id: string; receivedBytes: number; chunkSize: number }>(
            `/contracts/${contractId}/upload-sessions`,
            { purpose, fileName: file.name, fileSize: file.size },
          );
          sessionId = data.id;
          return data;
        },
        status: async (id) => {
          try {
            const { data } = await api.get<{ receivedBytes: number; chunkSize: number }>(
              `/upload-sessions/${id}`,
            );
            sessionId = id;
            return data;
          } catch (error) {
            if (axios.isAxiosError(error) && error.response?.status === 404) return null;
            throw error;
          }
        },
        put: async (id, offset, chunk, signal) => {
          try {
            const { data } = await api.put<{ receivedBytes: number }>(
              `/upload-sessions/${id}/chunks`,
              chunk,
              {
                params: { offset },
                headers: { "Content-Type": "application/octet-stream" },
                signal,
              },
            );
            return { kind: "stored", receivedBytes: data.receivedBytes };
          } catch (error) {
            return putResult(error);
          }
        },
      },
    });
    const response = await complete(sessionId);
    resumeStore.remove(resumeKey);
    uploadTracker.finish(trackerId);
    return response;
  } catch (error) {
    if (controller.signal.aborted) {
      // Cancelled from the tray: drop the staged bytes too.
      resumeStore.remove(resumeKey);
      if (sessionId) void api.delete(`/upload-sessions/${sessionId}`).catch(() => undefined);
    }
    uploadTracker.fail(trackerId, error);
    throw error;
  }
}
