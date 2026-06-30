## Context

The SkySim Logger Admin UI is a new frontend application that will provide a web interface for log lookup and flow monitoring. The backend Logger module is complete with:
- Kafka Consumer receiving action logs
- PostgreSQL storage with `log_flows`, `log_actions`, `log_action_details` tables
- Secured REST API endpoints for querying logs

This phase creates the frontend foundation only - project scaffolding, routing, folder structure, placeholder pages, and API client configuration. Detailed UI implementation from Stitch designs will be done in subsequent phases.

**Stakeholders**: SkySim backend team, operations team needing log visibility.

## Goals / Non-Goals

**Goals:**
- Create a React + TypeScript + Vite project under `frontend/`
- Configure TailwindCSS for styling
- Set up React Router with base routes for Login, Dashboard, Log List, and Log Detail pages
- Create placeholder pages with basic structure
- Create AdminLayout with sidebar/header placeholders
- Configure Axios instance with environment variable for Logger API base URL
- Provide clear documentation for running the frontend locally

**Non-Goals:**
- Detailed UI implementation (placeholder pages only)
- Keycloak authentication integration
- Protected route implementation
- API data loading and Redux state management
- Filters, pagination, search functionality
- Backend modifications
- Docker setup for frontend

## Decisions

### 1. Vite over Create React App
**Decision**: Use Vite as the build tool.
**Rationale**: Faster development server startup, hot module replacement, simpler configuration, and modern standard for React projects.
**Alternatives considered**: Create React App (deprecated), Next.js (overkill for admin UI).

### 2. React Router v6 for Routing
**Decision**: Use React Router v6.
**Rationale**: Standard routing library for React, declarative routes, nested routing support for layouts.
**Alternatives considered**: Wouter (lighter but less established), React Location (more features but complex).

### 3. Axios for HTTP Client
**Decision**: Use Axios for API calls.
**Rationale**: Familiar API, interceptors for auth/error handling, automatic JSON transformation.
**Alternatives considered**: Fetch API (no interceptors without wrapper), React Query (adds caching complexity not needed yet).

### 4. TailwindCSS for Styling
**Decision**: Use TailwindCSS for styling.
**Rationale**: Rapid UI development, consistent design system, easy responsive design.
**Alternatives considered**: CSS Modules (more manual), Styled Components (JS-in-CSS, less familiar), CSS Modules with AdminLayout.module.css (unnecessary complexity).

**Constraint**: All components use TailwindCSS utility classes only. No CSS modules or separate CSS files for components.

### 5. Folder Structure
**Decision**: Use structured folder organization.
```
src/
├── app/          # App entry, providers
├── components/   # Reusable UI components
├── layouts/      # Layout components (AdminLayout)
├── pages/        # Page components
├── services/     # API services
├── types/        # TypeScript interfaces
├── hooks/        # Custom React hooks
└── utils/        # Utility functions
```
**Rationale**: Clear separation of concerns, easy to locate files, scalable structure.

### 6. Environment Variables for Configuration
**Decision**: Use `.env` files with `VITE_` prefix.
**Rationale**: Vite natively supports `VITE_` prefixed variables, no additional config needed, easy to swap environments.
**Constraint**: Only `.env.example` is committed to git. Developers must copy `.env.example` to `.env` for local development. Both `.env` and `.env.local` must be ignored by git.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| TailwindCSS learning curve for team | Keep usage simple, use utility classes directly |
| Multiple placeholder pages | Placeholders are intentional for iterative development |
| No auth in initial phase | Authentication will be added in next phase |
| Backend API structure may change | Axios instance isolates API calls for easy updates |

## Open Questions

1. Should placeholder pages include mock data or be completely empty?
   - Decision: Completely empty with just page title for simplicity.

2. Will the Login page eventually use Keycloak redirect or form-based login?
   - Decision: To be determined in auth integration phase.

3. Should there be a logout mechanism in AdminLayout placeholder?
   - Decision: Include a basic logout button placeholder for future auth integration.
