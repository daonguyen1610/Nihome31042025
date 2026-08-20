import { test, expect, TEST_USERS } from "../fixtures/auth";
import type { APIRequestContext } from "@playwright/test";

/**
 * Full CRM pipeline E2E test: Lead → Customer + Opportunity → Quote → Contract
 * 
 * This test verifies the complete sales workflow from initial lead capture
 * to signed contract, including all intermediate states and transitions.
 */

function authed(api: APIRequestContext, token: string) {
  const auth = { Authorization: `Bearer ${token}` };
  return {
    get: (path: string) => api.get(path, { headers: auth }),
    post: (path: string, data: unknown) => api.post(path, { headers: auth, data }),
    put: (path: string, data: unknown) => api.put(path, { headers: auth, data }),
    patch: (path: string, data: unknown) => api.patch(path, { headers: auth, data }),
    del: (path: string) => api.delete(path, { headers: auth }),
  };
}

test.describe("CRM Pipeline: Lead → Opportunity → Quote → Contract", () => {
  test("complete sales cycle from lead to signed contract", async ({ api, loginAs }) => {
    test.setTimeout(60_000);

    const token = await loginAs(TEST_USERS.salesManager);
    const c = authed(api, token);
    const unique = Date.now().toString();

    // Cleanup IDs to delete at end
    let leadId = 0;
    let customerId = 0;
    let opportunityId = 0;
    let quoteId = 0;
    let contractId = 0;

    try {
      // ====== STEP 1: Create Lead ======
      console.log("Step 1: Creating lead...");
      const leadRes = await c.post("/api/leads", {
        name: `Pipeline Test Lead ${unique}`,
        companyName: "Pipeline Corp",
        phone: `09${unique.slice(-8)}`,
        email: `pipeline-${unique}@test.example`,
        sourceCode: "marketing",
      });
      expect(leadRes.status(), await leadRes.text()).toBe(201);
      const lead = await leadRes.json();
      leadId = lead.id as number;
      expect(lead.status).toBe("New");
      expect(lead.name).toContain("Pipeline Test Lead");

      // ====== STEP 2: Update Lead status to Contacted ======
      console.log("Step 2: Moving lead to Contacted...");
      const leadUpdateRes = await c.put(`/api/leads/${leadId}`, {
        name: lead.name,
        phone: lead.phone,
        sourceCode: "marketing",
        status: "Contacted",
        ownerUserId: lead.ownerUserId,
      });
      expect(leadUpdateRes.status(), await leadUpdateRes.text()).toBe(200);
      const updatedLead = await leadUpdateRes.json();
      expect(updatedLead.status).toBe("Contacted");

      // ====== STEP 3: Create Customer (for conversion) ======
      console.log("Step 3: Creating customer...");
      const customerRes = await c.post("/api/customers", {
        type: "Individual", // Simpler - no address required
        name: `Pipeline Customer ${unique}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: lead.name,
          phone: lead.phone,
          email: lead.email,
          isPrimary: true,
        },
      });
      expect(customerRes.status(), await customerRes.text()).toBe(201);
      const customer = await customerRes.json();
      customerId = customer.id as number;

      // ====== STEP 4: Create Opportunity ======
      console.log("Step 4: Creating opportunity...");
      const opportunityRes = await c.post("/api/opportunities", {
        name: `Pipeline Deal ${unique}`,
        customerId,
        estimatedValue: 500_000_000, // 500 million VND
        winProbability: 50,
      });
      expect(opportunityRes.status(), await opportunityRes.text()).toBe(201);
      const opportunity = await opportunityRes.json();
      opportunityId = opportunity.id as number;
      expect(opportunity.stage).toBe("Prospecting"); // Default stage

      // ====== STEP 5: Convert Lead (link to Customer + Opportunity) ======
      console.log("Step 5: Converting lead...");
      const convertRes = await c.post(`/api/leads/${leadId}/convert`, {
        customerId,
        opportunityId,
        note: "Customer interested in full package",
      });
      expect(convertRes.status(), await convertRes.text()).toBe(200);
      const convertedLead = await convertRes.json();
      expect(convertedLead.status).toBe("Converted");
      expect(convertedLead.convertedCustomerId).toBe(customerId);
      expect(convertedLead.convertedOpportunityId).toBe(opportunityId);

      // ====== STEP 6: Move Opportunity through stages ======
      console.log("Step 6: Advancing opportunity stages...");
      // New → Qualification
      let oppUpdate = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Qualification",
      });
      expect(oppUpdate.status(), await oppUpdate.text()).toBe(200);
      expect((await oppUpdate.json()).stage).toBe("Qualification");

      // Qualification → Proposal
      oppUpdate = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Proposal",
      });
      expect(oppUpdate.status(), await oppUpdate.text()).toBe(200);
      expect((await oppUpdate.json()).stage).toBe("Proposal");

      // ====== STEP 7: Create Quote for Opportunity ======
      console.log("Step 7: Creating quote...");
      const quoteRes = await c.post("/api/quotes", {
        opportunityId,
        method: "UnitCost",
        areaSqm: 150,
        unitPricePerSqm: 3_500_000, // 3.5M per sqm
        packageDescription: `Full construction package for ${customer.name}`,
        discountPercent: 5,
        vatPercent: 8,
        note: "Special discount for first-time customer",
      });
      expect(quoteRes.status(), await quoteRes.text()).toBe(201);
      const quote = await quoteRes.json();
      quoteId = quote.id as number;
      expect(quote.status).toBe("Draft");
      expect(quote.code).toMatch(/^QT-\d{4}-\d+$/); // e.g. QT-2026-0001

      // ====== STEP 8: Submit Quote for Approval ======
      console.log("Step 8: Submitting quote...");
      const submitRes = await c.post(`/api/quotes/${quoteId}/submit`, {});
      expect(submitRes.status(), await submitRes.text()).toBe(200);
      expect((await submitRes.json()).status).toBe("PendingApproval");

      // ====== STEP 9: Approve Quote ======
      console.log("Step 9: Approving quote...");
      const approveRes = await c.post(`/api/quotes/${quoteId}/approve`, {});
      expect(approveRes.status(), await approveRes.text()).toBe(200);
      const approvedQuote = await approveRes.json();
      expect(approvedQuote.status).toBe("Approved");

      // ====== STEP 10: Move Opportunity to Negotiation ======
      console.log("Step 10: Moving opportunity to Negotiation...");
      oppUpdate = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Negotiation",
      });
      expect(oppUpdate.status(), await oppUpdate.text()).toBe(200);
      expect((await oppUpdate.json()).stage).toBe("Negotiation");

      // ====== STEP 11: Create Contract from Quote ======
      console.log("Step 11: Creating contract...");
      const today = new Date().toISOString().split("T")[0];
      const startDate = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().split("T")[0]; // +7 days
      const endDate = new Date(Date.now() + 180 * 24 * 60 * 60 * 1000).toISOString().split("T")[0]; // +180 days

      const contractRes = await c.post("/api/contracts", {
        customerId,
        quoteId,
        signDate: today,
        startDate,
        endDate,
        contractValue: approvedQuote.grandTotal,
        paymentTerms: "30% upfront, 40% at foundation, 30% at completion",
        scopeOfWork: approvedQuote.packageDescription,
      });
      expect(contractRes.status(), await contractRes.text()).toBe(201);
      const contract = await contractRes.json();
      contractId = contract.id as number;
      // Contract may not have code field - check status only
      expect(contract.status).toBe("Draft");

      // ====== STEP 12: Sign Contract (activate) ======
      console.log("Step 12: Signing contract...");
      const signRes = await c.put(`/api/contracts/${contractId}`, {
        ...contract,
        status: "Signed",
      });
      expect(signRes.status(), await signRes.text()).toBe(200);
      const signedContract = await signRes.json();
      expect(signedContract.status).toBe("Signed");

      // ====== STEP 13: Mark Opportunity as Won ======
      console.log("Step 13: Marking opportunity as Won...");
      oppUpdate = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Won",
        wonQuoteId: quoteId,
      });
      expect(oppUpdate.status(), await oppUpdate.text()).toBe(200);
      expect((await oppUpdate.json()).stage).toBe("Won");

      // ====== VERIFY: Check full pipeline links ======
      console.log("Verifying pipeline links...");

      // Lead should link to Customer + Opportunity
      const finalLead = await c.get(`/api/leads/${leadId}`);
      expect(finalLead.status()).toBe(200);
      const finalLeadData = await finalLead.json();
      expect(finalLeadData.convertedCustomerId).toBe(customerId);
      expect(finalLeadData.convertedOpportunityId).toBe(opportunityId);

      // Opportunity should have correct data
      const finalOpp = await c.get(`/api/opportunities/${opportunityId}`);
      expect(finalOpp.status()).toBe(200);
      const finalOppData = await finalOpp.json();
      expect(finalOppData.customerId).toBe(customerId);
      expect(finalOppData.stage).toBe("Won");

      // Quote should link to opportunity
      const finalQuote = await c.get(`/api/quotes/${quoteId}`);
      expect(finalQuote.status()).toBe(200);
      const finalQuoteData = await finalQuote.json();
      expect(finalQuoteData.opportunityId).toBe(opportunityId);
      expect(finalQuoteData.status).toBe("Approved");

      // Contract should link to customer and quote
      const finalContract = await c.get(`/api/contracts/${contractId}`);
      expect(finalContract.status()).toBe(200);
      const finalContractData = await finalContract.json();
      expect(finalContractData.customerId).toBe(customerId);
      expect(finalContractData.quoteId).toBe(quoteId);
      expect(finalContractData.status).toBe("Signed");

      console.log("✓ Full CRM pipeline test completed successfully!");

    } finally {
      // Cleanup in reverse order
      if (contractId) await c.del(`/api/contracts/${contractId}`).catch(() => {});
      if (quoteId) await c.del(`/api/quotes/${quoteId}`).catch(() => {});
      if (opportunityId) await c.del(`/api/opportunities/${opportunityId}`).catch(() => {});
      if (customerId) await c.del(`/api/customers/${customerId}`).catch(() => {});
      if (leadId) await c.del(`/api/leads/${leadId}`).catch(() => {});
    }
  });

  test("convert lead auto-creates customer when not provided", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const c = authed(api, token);
    const unique = Date.now().toString();

    let leadId = 0;
    let autoCreatedCustomerId = 0;

    try {
      // Create lead with contact info
      const leadRes = await c.post("/api/leads", {
        name: `Auto-Convert Lead ${unique}`,
        companyName: `Auto Corp ${unique}`,
        phone: `08${unique.slice(-8)}`,
        email: `auto-${unique}@test.example`,
        sourceCode: "marketing",
      });
      expect(leadRes.status()).toBe(201);
      const lead = await leadRes.json();
      leadId = lead.id as number;

      // Convert without providing customerId - should auto-create
      const convertRes = await c.post(`/api/leads/${leadId}/convert`, {
        note: "Auto-create customer test",
      });
      expect(convertRes.status(), await convertRes.text()).toBe(200);
      const convertedLead = await convertRes.json();
      expect(convertedLead.status).toBe("Converted");
      expect(convertedLead.convertedCustomerId).toBeTruthy();
      autoCreatedCustomerId = convertedLead.convertedCustomerId as number;

      // Verify customer was created with lead data
      const customerRes = await c.get(`/api/customers/${autoCreatedCustomerId}`);
      expect(customerRes.status()).toBe(200);
      const customer = await customerRes.json();
      expect(customer.name).toBe(`Auto Corp ${unique}`); // Uses companyName
      expect(customer.sourceCode).toBe("marketing");

      // Verify primary contact has lead info
      const primaryContact = customer.contacts?.find((c: { isPrimary: boolean }) => c.isPrimary);
      expect(primaryContact).toBeTruthy();
      expect(primaryContact.fullName).toBe(`Auto-Convert Lead ${unique}`);
      expect(primaryContact.phone).toBe(`08${unique.slice(-8)}`);
      expect(primaryContact.email).toBe(`auto-${unique}@test.example`);

      console.log("✓ Auto-create customer from lead test passed!");

    } finally {
      if (autoCreatedCustomerId) await c.del(`/api/customers/${autoCreatedCustomerId}`).catch(() => {});
      if (leadId) await c.del(`/api/leads/${leadId}`).catch(() => {});
    }
  });

  test("lost opportunity flow with required reason", async ({ api, loginAs }) => {
    const token = await loginAs(TEST_USERS.salesManager);
    const c = authed(api, token);
    const unique = Date.now().toString();

    let customerId = 0;
    let opportunityId = 0;

    try {
      // Create customer
      const customerRes = await c.post("/api/customers", {
        type: "Individual",
        name: `Lost Test Customer ${unique}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "Lost Contact",
          phone: `08${unique.slice(-8)}`,
          isPrimary: true,
        },
      });
      expect(customerRes.status()).toBe(201);
      customerId = ((await customerRes.json()).id as number);

      // Create opportunity
      const oppRes = await c.post("/api/opportunities", {
        name: `Lost Deal ${unique}`,
        customerId,
        estimatedValue: 100_000_000,
        winProbability: 30,
      });
      expect(oppRes.status()).toBe(201);
      opportunityId = ((await oppRes.json()).id as number);

      // Try to mark as Lost without reason - should fail
      const lostNoReason = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Lost",
      });
      expect(lostNoReason.status()).toBe(400);

      // Mark as Lost with reason - should succeed
      const lostWithReason = await c.patch(`/api/opportunities/${opportunityId}/stage`, {
        targetStage: "Lost",
        lostReasonCode: "price",
        lostNote: "Customer found cheaper alternative",
      });
      expect(lostWithReason.status()).toBe(200);
      const lostOpp = await lostWithReason.json();
      expect(lostOpp.stage).toBe("Lost");
      expect(lostOpp.lostReasonCode).toBe("price");

    } finally {
      if (opportunityId) await c.del(`/api/opportunities/${opportunityId}`).catch(() => {});
      if (customerId) await c.del(`/api/customers/${customerId}`).catch(() => {});
    }
  });

  test("SALE role cannot access other owners' pipeline data", async ({ api, loginAs }) => {
    const managerToken = await loginAs(TEST_USERS.salesManager);
    const saleToken = await loginAs(TEST_USERS.sale);
    const manager = authed(api, managerToken);
    const sale = authed(api, saleToken);
    const unique = Date.now().toString();

    let customerId = 0;
    let opportunityId = 0;

    try {
      // Manager creates customer and opportunity
      const customerRes = await manager.post("/api/customers", {
        type: "Individual",
        name: `RBAC Test Customer ${unique}`,
        sourceCode: "marketing",
        primaryContact: {
          fullName: "RBAC Contact",
          phone: `07${unique.slice(-8)}`,
          isPrimary: true,
        },
      });
      expect(customerRes.status()).toBe(201);
      customerId = ((await customerRes.json()).id as number);

      const oppRes = await manager.post("/api/opportunities", {
        name: `RBAC Deal ${unique}`,
        customerId,
        estimatedValue: 200_000_000,
        winProbability: 40,
      });
      expect(oppRes.status()).toBe(201);
      opportunityId = ((await oppRes.json()).id as number);

      // SALE should NOT see manager's opportunity
      const saleGet = await sale.get(`/api/opportunities/${opportunityId}`);
      expect(saleGet.status()).toBe(404); // Hidden, not forbidden

      // SALE list should not include it
      const saleList = await sale.get("/api/opportunities?pageSize=100");
      expect(saleList.status()).toBe(200);
      const items = (await saleList.json()).items as Array<{ id: number }>;
      expect(items.some(o => o.id === opportunityId)).toBe(false);

    } finally {
      if (opportunityId) await manager.del(`/api/opportunities/${opportunityId}`).catch(() => {});
      if (customerId) await manager.del(`/api/customers/${customerId}`).catch(() => {});
    }
  });
});
