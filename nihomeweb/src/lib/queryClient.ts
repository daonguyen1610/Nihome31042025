import { QueryClient } from "@tanstack/react-query";

/**
 * Global QueryClient instance shared across the app.
 * Exported so that thunks and other non-component code can clear/invalidate queries.
 */
export const queryClient = new QueryClient();
