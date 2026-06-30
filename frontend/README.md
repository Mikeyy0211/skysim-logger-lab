# SkySim Logger Web

Frontend admin UI for the SkySim Logger module, built with React + TypeScript + Vite.

## Prerequisites

- Node.js 18+
- npm 9+

## Setup

1. Install dependencies:

```bash
npm install
```

2. Copy `.env.example` to `.env`:

```bash
cp .env.example .env
```

3. Update `.env` with your environment values if needed.

## Development

Start the development server:

```bash
npm run dev
```

The app will be available at `http://localhost:5173`

## Routes

- `/login` - Login page
- `/dashboard` - Dashboard
- `/logs` - Flow monitoring list
- `/logs/:flowId` - Flow detail view

## Build

Create a production build:

```bash
npm run build
```

Preview the production build:

```bash
npm run preview
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `VITE_LOGGER_API_BASE_URL` | Logger API base URL | `http://localhost:5108` |
| `VITE_KEYCLOAK_BASE_URL` | Keycloak server URL | `http://localhost:8081` |
| `VITE_KEYCLOAK_REALM` | Keycloak realm | `skysim` |
| `VITE_KEYCLOAK_CLIENT_ID` | Keycloak client ID | `skysim-logger-api` |
