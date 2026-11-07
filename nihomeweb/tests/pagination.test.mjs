import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/lib/pagination.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.ES2022, target: ts.ScriptTarget.ES2022 },
}).outputText;
const { collectLookupPages, findRecordPage } = await import(
  "data:text/javascript;base64," + Buffer.from(compiled).toString("base64")
);

test("lookup collection follows the server's smaller page size", async () => {
  const calls = [];
  const rows = [{ id: 8 }, { id: 12 }, { id: 17 }, { id: 19 }, { id: 23 }];
  const result = await collectLookupPages(async (page) => {
    calls.push(page);
    return {
      data: { total: rows.length, page, pageSize: 2, items: rows.slice((page - 1) * 2, page * 2) },
    };
  });
  assert.deepEqual(calls, [1, 2, 3]);
  assert.deepEqual(result.data.items, rows);
});

test("lookup collection propagates later-page errors instead of returning incomplete choices", async () => {
  await assert.rejects(
    collectLookupPages(async (page) => {
      if (page === 2) throw new Error("lookup unavailable");
      return { data: { total: 2, page, pageSize: 1, items: [{ id: 1 }] } };
    }),
    /lookup unavailable/,
  );
});

test("lookup collection rejects repeated pages instead of hiding incomplete choices", async () => {
  await assert.rejects(
    collectLookupPages(async (page) => ({
      data: { total: 3, page, pageSize: 2, items: [{ id: 1 }, { id: 2 }] },
    })),
    (error) => error.code === "LOOKUP_INCOMPLETE",
  );
});

test("lookup collection preserves work-package totalCount metadata", async () => {
  const result = await collectLookupPages(async (page) => ({
    data: { totalCount: 2, page, pageSize: 1, items: [{ id: page, code: `WP-${page}` }] },
  }));
  assert.equal(result.data.totalCount, 2);
  assert.deepEqual(
    result.data.items.map((item) => item.code),
    ["WP-1", "WP-2"],
  );
});

test("routed documents locate their later page without loading outside the filtered result", async () => {
  const calls = [];
  const load = async (page) => {
    calls.push(page);
    return {
      data: {
        total: 5,
        page,
        pageSize: 2,
        items: [1, 2, 3, 4, 5].slice((page - 1) * 2, page * 2).map((id) => ({ id })),
      },
    };
  };
  assert.equal(await findRecordPage(load, 5), 3);
  assert.deepEqual(calls, [1, 2, 3]);
  calls.length = 0;
  assert.equal(await findRecordPage(load, 99), null);
  assert.deepEqual(calls, [1, 2, 3]);
});
