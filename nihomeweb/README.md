## Nihome Web

This is the Next.js frontend for the Nihome platform.

## Development

Create your local environment file:

```bash
cp .env.example .env.local
```

Then run the app:

```bash
npm run dev
```

Open `http://localhost:3000`.

## Backend proxy

The app proxies `/api/*` requests to the ASP.NET backend using `BACKEND_URL`.

Default local value:

```env
BACKEND_URL=http://localhost:5067
```

Example:

```ts
await fetch("/api/system/health");
```

## Main files

- `app/page.tsx`: landing page and backend status check
- `app/layout.tsx`: app metadata and fonts
- `next.config.ts`: API rewrite to the backend

From here, you can start adding dashboard, auth, tenant, and property screens under `app/`.
