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
  const operationalProjectResponse = await api.post("/api/operational-projects", {
    headers: options.headers,
    data: {
      name: `${options.name} operational project`,
      customerId: options.customerId,
    },
  });
  if (!operationalProjectResponse.ok()) {
    throw new Error(
      `Operational project creation failed (${operationalProjectResponse.status()}): ${await operationalProjectResponse.text()}`,
    );
  }
  const operationalProjectId = (await operationalProjectResponse.json()).id as number;
  let lastError = "";

  for (let attempt = 1; attempt <= 5; attempt += 1) {
    const response = await api.post("/api/design-projects", {
      headers: options.headers,
      data: {
        name: options.name,
        customerId: options.customerId,
        operationalProjectId,
      },
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

/**
 * Creates a customer this spec alone owns.
 *
 * Specs used to take the newest customer from the list instead. Customers come
 * back newest first, so that picked up whatever another spec had just created,
 * and deleting a customer cascades through AggregateDeletionService to its
 * design projects — so the other spec's cleanup silently removed the project
 * this one was working on, and the failure surfaced far from its cause.
 */
export async function createOwnCustomer(
  api: APIRequestContext,
  headers: Record<string, string>,
  label: string,
): Promise<number> {
  const unique = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`;
  const response = await api.post("/api/customers", {
    headers,
    data: {
      name: `E2E ${label} customer ${unique}`,
      type: "Company",
      sourceCode: "referral",
      relationshipStatus: "InProgress",
      taxId: `04${Math.floor(10_000_000 + Math.random() * 89_999_999)}`,
      address: "88 Lý Thường Kiệt, Hà Nội",
      representativeName: `${label} Rep ${unique}`,
      primaryContact: {
        fullName: `${label} Rep ${unique}`,
        phone: `098${Math.floor(1_000_000 + Math.random() * 8_999_999)}`,
        email: `e2e-${unique}@nihome.test`,
        isPrimary: true,
      },
    },
  });

  if (!response.ok()) {
    throw new Error(`Customer creation failed (${response.status()}): ${await response.text()}`);
  }

  return (await response.json()).id as number;
}
