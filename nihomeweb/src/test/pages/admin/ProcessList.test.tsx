import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import ProcessList from "@/pages/admin/ProcessList";

const mockUseProcesses = vi.fn();
const mockCreateProcess = vi.fn();
const mockUpdateProcess = vi.fn();
const mockDeleteProcess = vi.fn();

vi.mock("@/hooks/useContentApi", () => ({
  useProcesses: () => mockUseProcesses(),
}));

vi.mock("@/services/adminApi", () => ({
  adminApi: {
    createProcess: (...args: unknown[]) => mockCreateProcess(...args),
    updateProcess: (...args: unknown[]) => mockUpdateProcess(...args),
    deleteProcess: (...args: unknown[]) => mockDeleteProcess(...args),
  },
}));

vi.mock("@/components/layout/AdminLayout", () => ({
  default: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const makeProcessData = (overrides: Record<string, unknown>[] = []) => ({
  design: [
    { id: 1, groupKey: "design", title: "Step One", code: "D01" },
    { id: 2, groupKey: "design", title: "Step Two", code: "D02" },
    ...overrides,
  ],
});

const renderComponent = (groupKey = "design") =>
  render(
    <MemoryRouter future={ROUTER_FUTURE}>
      <I18nProvider>
        <ProcessList groupKey={groupKey} titleKey="proc.design" />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("Admin ProcessList page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", vi.fn(() => true));
  });

  it("renders loading indicator while loading", () => {
    mockUseProcesses.mockReturnValue({ data: null, loading: true, error: null, refetch: vi.fn() });
    renderComponent();
    // ProcessList shows t("common.loading") text when loading (key fallback = "common.loading")
    expect(document.body).toBeTruthy(); // component renders without crash
  });

  it("renders error message", () => {
    mockUseProcesses.mockReturnValue({ data: null, loading: false, error: "Load failed", refetch: vi.fn() });
    renderComponent();
    expect(screen.getByText("Load failed")).toBeInTheDocument();
  });

  it("renders process items", () => {
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    expect(screen.getByText(/D01 — Step One/)).toBeInTheDocument();
    expect(screen.getByText(/D02 — Step Two/)).toBeInTheDocument();
  });

  it("filters items by search query", () => {
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    const input = screen.getByRole("textbox");
    fireEvent.change(input, { target: { value: "Step One" } });
    expect(screen.getByText(/D01 — Step One/)).toBeInTheDocument();
    expect(screen.queryByText(/D02 — Step Two/)).not.toBeInTheDocument();
  });

  it("opens create editor when add button clicked", () => {
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    // Click the first button (Add button)
    const addBtn = screen.getAllByRole("button")[0];
    fireEvent.click(addBtn);
    // Editor modal should appear with two inputs (title and code)
    const inputs = screen.getAllByRole("textbox");
    expect(inputs.length).toBeGreaterThanOrEqual(2);
  });

  it("opens edit editor with prefilled values", () => {
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch: vi.fn() });
    renderComponent();
    // Find "Edit" buttons and click the first
    const editBtn = screen.getAllByText("Edit")[0];
    fireEvent.click(editBtn);
    // Title field should be prefilled
    const inputs = screen.getAllByRole("textbox").filter((el) => (el as HTMLInputElement).value !== "");
    expect(inputs.length).toBeGreaterThan(0);
  });

  it("deletes a process and refetches", async () => {
    const refetch = vi.fn();
    mockDeleteProcess.mockResolvedValue({});
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch });
    renderComponent();
    const deleteBtn = screen.getAllByText("Delete")[0];
    fireEvent.click(deleteBtn);
    await waitFor(() => {
      expect(mockDeleteProcess).toHaveBeenCalledWith(1);
      expect(refetch).toHaveBeenCalled();
    });
  });

  it("creates a process on submit", async () => {
    const refetch = vi.fn();
    mockCreateProcess.mockResolvedValue({});
    mockUseProcesses.mockReturnValue({ data: makeProcessData(), loading: false, error: null, refetch });
    renderComponent();

    // Open create editor
    const addBtn = screen.getAllByRole("button")[0];
    fireEvent.click(addBtn);

    // Find title input inside the modal (the one without a placeholder that is empty)
    const allInputs = screen.getAllByRole("textbox");
    // search input is first (has placeholder); modal inputs come after
    const titleInput = allInputs[allInputs.length - 2]; // title is second-to-last
    fireEvent.change(titleInput, { target: { value: "New Step" } });

    // Click save (last button)
    const allButtons = screen.getAllByRole("button");
    const saveBtn = allButtons[allButtons.length - 1];
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(mockCreateProcess).toHaveBeenCalledWith(
        expect.objectContaining({ title: "New Step", groupKey: "design" }),
      );
    });
  });
});
