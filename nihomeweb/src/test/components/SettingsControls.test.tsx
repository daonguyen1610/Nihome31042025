import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { Toggle } from "@/components/admin/SettingsControls";

describe("SettingsControls Toggle", () => {
  it("uses a fallback accessible name when ariaLabel is not provided", () => {
    render(<Toggle on={false} onChange={vi.fn()} />);

    expect(screen.getByRole("button", { name: "Toggle setting" })).toBeInTheDocument();
  });

  it("prefers ariaLabel when provided", () => {
    const onChange = vi.fn();
    render(<Toggle on={false} onChange={onChange} ariaLabel="Enable OTP for registration" />);

    fireEvent.click(screen.getByRole("button", { name: "Enable OTP for registration" }));

    expect(onChange).toHaveBeenCalledWith(true);
  });
});
