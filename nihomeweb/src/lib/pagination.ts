type LookupPage = {
  items: { id: number }[];
  total?: number;
  totalCount?: number;
  page: number;
  pageSize: number;
};

/** Locate a routed record using the same scope/filters as its visible list. */
export async function findRecordPage<P extends LookupPage>(
  load: (page: number) => Promise<{ data: P }>,
  id: number,
): Promise<number | null> {
  for (let page = 1; ; page++) {
    const { data } = await load(page);
    if (data.items.some((item) => item.id === id)) return page;
    if (data.items.length === 0 || page * data.pageSize >= (data.total ?? data.totalCount ?? 0))
      return null;
  }
}

/** For option/reference sets only; visible record lists must paginate. */
export async function collectLookupPages<P extends LookupPage>(
  load: (page: number) => Promise<{ data: P }>,
): Promise<{ data: P }> {
  const first = await load(1);
  const items = [...first.data.items];
  const total = first.data.total ?? first.data.totalCount ?? 0;
  for (let page = 2; items.length < total; page++) {
    const next = await load(page);
    const seen = new Set(items.map((item) => item.id));
    const added = next.data.items.filter((item) => !seen.has(item.id));
    if (added.length === 0) {
      throw Object.assign(new Error("Reference options could not be fully loaded"), {
        code: "LOOKUP_INCOMPLETE",
      });
    }
    items.push(...added);
  }
  return { data: { ...first.data, items } };
}
