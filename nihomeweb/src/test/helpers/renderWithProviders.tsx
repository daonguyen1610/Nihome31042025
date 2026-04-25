import { type ReactNode } from "react";
import { render, type RenderOptions } from "@testing-library/react";
import { configureStore } from "@reduxjs/toolkit";
import { Provider } from "react-redux";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "@/lib/i18n";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Toaster } from "@/components/ui/toaster";
import authReducer from "@/store/authSlice";

type AuthPreloadState = Parameters<typeof configureStore>[0] extends { preloadedState?: infer P } ? P : never;

interface RenderWithProvidersOptions extends Omit<RenderOptions, "wrapper"> {
  preloadedState?: AuthPreloadState;
  route?: string;
}

export function renderWithProviders(ui: ReactNode, options: RenderWithProvidersOptions = {}) {
  const { preloadedState, route = "/", ...renderOptions } = options;

  const store = configureStore({
    reducer: { auth: authReducer },
    preloadedState,
  });

  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <Provider store={store}>
        <I18nProvider>
          <TooltipProvider>
            <Toaster />
            <MemoryRouter initialEntries={[route]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>{children}</MemoryRouter>
          </TooltipProvider>
        </I18nProvider>
      </Provider>
    );
  }

  return { store, ...render(ui, { wrapper: Wrapper, ...renderOptions }) };
}
