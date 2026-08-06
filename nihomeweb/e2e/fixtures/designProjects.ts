import type { APIRequestContext } from "@playwright/test";

interface CreateDesignProjectOptions {
  headers: Record<string, string>;
  name: string;
  customerId: number;
}

export async function createDesignProject(
  api: APIRequestContext,
  options: CreateDesignProjectOptions,
): Promise<number> {
  let lastError = "";

  for (let attempt = 1; attempt <= 5; attempt += 1) {
    const response = await api.post("/api/design-projects", {
      headers: options.headers,
      data: { name: options.name, customerId: options.customerId },
    });

    if (response.ok()) {
      return (await response.json()).id as number;
    }

    lastError = await response.text();
    if (response.status() !== 409) {
      throw new Error(`Design project creation failed (${response.status()}): ${lastError}`);
    }

    await new Promise((resolve) => setTimeout(resolve, attempt * 100));
  }

  throw new Error(`Design project creation still conflicted after 5 attempts: ${lastError}`);
}
