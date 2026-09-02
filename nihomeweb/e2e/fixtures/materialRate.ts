import { expect, type APIRequestContext } from "@playwright/test";

interface InvestmentRateFixture {
  catalogId: number;
  catalogCode: string;
  catalogName: string;
  pricingEffectiveDate: string;
}

export async function createApprovedInvestmentRate(
  api: APIRequestContext,
  headers: { Authorization: string },
  suffix: string,
  unitPricePerSqm: number,
): Promise<InvestmentRateFixture> {
  const catalogCode = `E2E-INV-${suffix}`.slice(0, 60);
  const catalogName = `E2E investment rate ${suffix}`;
  const catalogResponse = await api.post("/api/material-rate-catalogs", {
    headers,
    data: {
      catalogType: "InvestmentRate",
      code: catalogCode,
      name: catalogName,
      currency: "VND",
      isActive: true,
    },
  });
  expect(catalogResponse.status(), await catalogResponse.text()).toBe(201);
  const catalogId = ((await catalogResponse.json()).id as number);

  const revisionResponse = await api.post(`/api/material-rate-catalogs/${catalogId}/revisions`, {
    headers,
    data: { effectiveFrom: "2020-01-01", effectiveTo: "2099-12-31" },
  });
  expect(revisionResponse.status(), await revisionResponse.text()).toBe(201);
  const revisionId = ((await revisionResponse.json()).id as number);
  const csv = [
    "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent",
    `E2E-PACKAGE,Construction package,m2,1,${unitPricePerSqm},0`,
    "",
  ].join("\r\n");
  const importResponse = await api.post(
    `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`,
    {
      headers,
      multipart: {
        file: { name: `investment-rate-${suffix}.csv`, mimeType: "text/csv", buffer: Buffer.from(csv) },
      },
    },
  );
  expect(importResponse.status(), await importResponse.text()).toBe(200);

  const approveResponse = await api.post(
    `/api/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve`,
    { headers, data: { note: "Approved E2E investment rate fixture" } },
  );
  expect(approveResponse.status(), await approveResponse.text()).toBe(200);

  return {
    catalogId,
    catalogCode,
    catalogName,
    pricingEffectiveDate: new Date().toISOString().slice(0, 10),
  };
}

export async function retireInvestmentRate(
  api: APIRequestContext,
  headers: { Authorization: string },
  fixture: InvestmentRateFixture,
) {
  const response = await api.put(`/api/material-rate-catalogs/${fixture.catalogId}`, {
    headers,
    data: {
      catalogType: "InvestmentRate",
      code: fixture.catalogCode,
      name: fixture.catalogName,
      currency: "VND",
      isActive: false,
    },
  });
  expect(response.status(), await response.text()).toBe(200);
}
