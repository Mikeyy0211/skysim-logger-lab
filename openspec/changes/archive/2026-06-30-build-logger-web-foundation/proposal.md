## Why

The backend Logger module (Kafka Consumer, PostgreSQL storage, secured REST API) is complete enough for E2E demonstration. The frontend foundation is needed to provide an Admin UI for log lookup and flow monitoring. This phase establishes the project scaffolding, routing, and folder structure to enable iterative UI development.

## What Changes

- Create new `frontend/` directory with React + TypeScript + Vite project
- Configure TailwindCSS for styling
- Set up React Router with 4 base routes: `/login`, `/dashboard`, `/logs`, `/logs/:flowId`
- Create placeholder pages: `LoginPage`, `DashboardPage`, `LogListPage`, `LogDetailPage`
- Create `AdminLayout` with sidebar/header for authenticated pages
- Configure Axios instance with environment variable for API base URL
- Add `.env.example` with required environment variables
- Add README with local run instructions

## Capabilities

### New Capabilities
- `logger-web-foundation`: Frontend foundation with routing, folder structure, placeholder pages, and API client configuration

### Modified Capabilities
<!-- No existing capabilities being modified -->

## Impact

- **New code**: `frontend/` directory with React application
- **Dependencies**: React, TypeScript, Vite, TailwindCSS, React Router, Axios
- **Configuration**: `.env.example`, `tsconfig.json`, `vite.config.ts`, `tailwind.config.js`
- **No backend changes**: Backend code, Docker, Kafka, Keycloak, PostgreSQL remain unchanged
