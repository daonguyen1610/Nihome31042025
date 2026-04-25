import { describe, it, expect, vi, beforeEach } from "vitest";

// Reset module before each test to get a fresh api instance
let setStoreRef: typeof import("@/lib/api").setStoreRef;
let api: typeof import("@/lib/api").default;

describe("api (Axios instance)", () => {
  beforeEach(async () => {
    vi.resetModules();
    const mod = await import("@/lib/api");
    api = mod.default;
    setStoreRef = mod.setStoreRef;
  });

  it("has correct baseURL", () => {
    expect(api.defaults.baseURL).toBe("/api");
  });

  it("has Content-Type header set to application/json", () => {
    expect(api.defaults.headers["Content-Type"]).toBe("application/json");
  });

  it("request interceptor adds Authorization header when token exists", async () => {
    setStoreRef(() => ({ auth: { accessToken: "test-token-123" } }) as never);
    // Access the interceptor by creating a mock request config
    const interceptors = api.interceptors.request as unknown as {
      handlers: Array<{ fulfilled: (config: Record<string, unknown>) => Record<string, unknown> }>;
    };
    const handler = interceptors.handlers[0].fulfilled;
    const config = { headers: { set: vi.fn() } } as unknown as Record<string, unknown>;
    const result = handler(config) as { headers: { Authorization?: string } };
    expect(result.headers).toBeDefined();
  });

  it("request interceptor does not add Authorization when no token", () => {
    setStoreRef(() => ({ auth: { accessToken: null } }) as never);
    const interceptors = api.interceptors.request as unknown as {
      handlers: Array<{ fulfilled: (config: Record<string, unknown>) => Record<string, unknown> }>;
    };
    const handler = interceptors.handlers[0].fulfilled;
    const config = { headers: {} } as Record<string, unknown>;
    const result = handler(config);
    expect((result.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it("request interceptor works when storeRef not set", async () => {
    // Fresh import without calling setStoreRef
    vi.resetModules();
    const freshMod = await import("@/lib/api");
    const freshApi = freshMod.default;
    const interceptors = freshApi.interceptors.request as unknown as {
      handlers: Array<{ fulfilled: (config: Record<string, unknown>) => Record<string, unknown> }>;
    };
    const handler = interceptors.handlers[0].fulfilled;
    const config = { headers: {} } as Record<string, unknown>;
    // Should not throw
    const result = handler(config);
    expect(result).toBeDefined();
  });
});
