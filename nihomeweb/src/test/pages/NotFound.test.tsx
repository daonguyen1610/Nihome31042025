import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import NotFound from "@/pages/NotFound";

const ROUTER_FUTURE = { v7_startTransition: true, v7_relativeSplatPath: true } as const;

const renderPage = (path = "/not-a-real-page") =>
  render(
    <MemoryRouter initialEntries={[path]} future={ROUTER_FUTURE}>
      <I18nProvider>
        <NotFound />
      </I18nProvider>
    </MemoryRouter>,
  );

describe("NotFound page", () => {
  it("renders 404 heading", () => {
    renderPage();
    expect(screen.getByText("404")).toBeInTheDocument();
  });

  it("renders oops message", () => {
    renderPage();
    expect(screen.getByText("Oops! Page not found")).toBeInTheDocument();
  });

  it("renders link to home", () => {
    renderPage();
    const homeLink = screen.getByRole("link", { name: "Return to home" });
    expect(homeLink).toHaveAttribute("href", "/");
  });
});
