import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";

describe("PageLoading", () => {
  it("renders a spinner element", () => {
    const { container } = render(<PageLoading />);
    expect(container.querySelector(".animate-spin")).toBeInTheDocument();
  });
});

describe("PageError", () => {
  it("renders the error message", () => {
    render(<PageError message="Something went wrong" />);
    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
  });

  it("does not render retry button when onRetry is not provided", () => {
    render(<PageError message="Error" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("renders retry button when onRetry is provided", () => {
    render(<PageError message="Error" onRetry={vi.fn()} />);
    expect(screen.getByRole("button")).toBeInTheDocument();
  });

  it("calls onRetry when retry button is clicked", () => {
    const onRetry = vi.fn();
    render(<PageError message="Error" onRetry={onRetry} />);
    fireEvent.click(screen.getByRole("button"));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

describe("PageEmpty", () => {
  it("renders the empty message", () => {
    render(<PageEmpty message="No items found" />);
    expect(screen.getByText("No items found")).toBeInTheDocument();
  });
});
