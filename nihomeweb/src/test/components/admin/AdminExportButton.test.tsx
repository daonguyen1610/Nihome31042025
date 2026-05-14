import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import AdminExportButton from "@/components/admin/AdminExportButton";

describe("AdminExportButton", () => {
  it("calls export handler when enabled", () => {
    const onClick = vi.fn();
    render(<AdminExportButton onClick={onClick} />);

    fireEvent.click(screen.getByRole("button", { name: /export excel/i }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("does not call export handler when disabled", () => {
    const onClick = vi.fn();
    render(<AdminExportButton onClick={onClick} disabled />);

    const button = screen.getByRole("button", { name: /export excel/i });
    expect(button).toBeDisabled();

    fireEvent.click(button);

    expect(onClick).not.toHaveBeenCalled();
  });
});
