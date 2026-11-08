import { expect, type APIRequestContext } from "@playwright/test";

export async function uploadSignedContractScan(
  api: APIRequestContext,
  headers: Record<string, string>,
  contractId: number,
): Promise<number> {
  const response = await api.post(`/api/contracts/${contractId}/attachments`, {
    headers,
    multipart: {
      kind: "SignedScan",
      file: {
        name: "NICON-hop-dong-da-ky.pdf",
        mimeType: "application/pdf",
        buffer: Buffer.from("%PDF-1.4\nNICON signed contract fixture\n%%EOF"),
      },
    },
  });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()).id as number;
}
