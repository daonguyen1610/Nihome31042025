/** Filters every "Chờ tôi duyệt" list shares; list-specific fields extend this. */
export type BaseApprovalFilters = {
  search: string;
  designProjectId: number | null;
  newestFirst: boolean;
};

export const EMPTY_BASE_FILTERS: BaseApprovalFilters = {
  search: "",
  designProjectId: null,
  newestFirst: false,
};

export const hasBaseFilters = (filters: BaseApprovalFilters) =>
  filters.search.trim() !== "" || filters.designProjectId != null || filters.newestFirst;
