# logger-web-foundation Specification

## Purpose
TBD - created by archiving change build-logger-web-foundation. Update Purpose after archive.
## Requirements
### Requirement: Frontend project scaffolding
The system SHALL provide a React + TypeScript + Vite frontend project under the `frontend/` directory with all necessary configuration files.

#### Scenario: Project initialization
- **WHEN** a developer runs the initialization commands
- **THEN** a complete React + TypeScript + Vite project is created with `package.json`, `tsconfig.json`, `vite.config.ts`, and `index.html`

#### Scenario: TailwindCSS configuration
- **WHEN** the project is initialized
- **THEN** TailwindCSS is configured with proper PostCSS setup in `tailwind.config.js` and `postcss.config.js`

#### Scenario: Environment configuration
- **WHEN** the project is initialized
- **THEN** a `.env.example` file exists with required environment variables:
  - `VITE_LOGGER_API_BASE_URL=http://localhost:5108`
  - `VITE_KEYCLOAK_BASE_URL=http://localhost:8081`
  - `VITE_KEYCLOAK_REALM=skysim`
  - `VITE_KEYCLOAK_CLIENT_ID=skysim-logger-api`
- **AND** `.env` and `.env.local` are ignored by git

### Requirement: Folder structure
The system SHALL provide a clean folder structure under `src/` with the following directories:
- `app/` - App entry point and providers
- `components/` - Reusable UI components
- `layouts/` - Layout components
- `pages/` - Page components
- `services/` - API services
- `types/` - TypeScript interfaces
- `hooks/` - Custom React hooks
- `utils/` - Utility functions

#### Scenario: Directory creation
- **WHEN** the project is initialized
- **THEN** all specified directories exist under `src/`

### Requirement: Base routing
The system SHALL provide React Router v6 configuration with the following routes:
- `/login` - Login page (no layout)
- `/dashboard` - Dashboard page (AdminLayout)
- `/logs` - Log list page (AdminLayout)
- `/logs/:flowId` - Log detail page (AdminLayout)

#### Scenario: Route configuration
- **WHEN** the router is configured
- **THEN** all routes are accessible and render the corresponding page components

#### Scenario: Nested routing
- **WHEN** authenticated pages are accessed
- **THEN** they render within the AdminLayout wrapper

### Requirement: Placeholder pages
The system SHALL provide placeholder page components for all routes with basic structure.

#### Scenario: Login page
- **WHEN** `/login` is accessed
- **THEN** a LoginPage component renders with a page title "Login"

#### Scenario: Dashboard page
- **WHEN** `/dashboard` is accessed
- **THEN** a DashboardPage component renders with a page title "Dashboard"

#### Scenario: Log list page
- **WHEN** `/logs` is accessed
- **THEN** a LogListPage component renders with a page title "Flow Monitoring"

#### Scenario: Log detail page
- **WHEN** `/logs/:flowId` is accessed
- **THEN** a LogDetailPage component renders with a page title "Flow Detail" and displays the flowId from route params

### Requirement: AdminLayout component
The system SHALL provide an AdminLayout component for authenticated pages with:
- Sidebar navigation with working links to Dashboard and Logs
- Header with logout button placeholder
- Main content area
- Styled using TailwindCSS utility classes only (no CSS modules)

#### Scenario: AdminLayout structure
- **WHEN** AdminLayout renders
- **THEN** it displays a sidebar, header, and main content area styled with TailwindCSS

#### Scenario: Logout placeholder
- **WHEN** AdminLayout renders
- **THEN** a logout button placeholder exists for future auth integration

### Requirement: Axios instance configuration
The system SHALL provide a configured Axios instance for API calls.

#### Scenario: API client creation
- **WHEN** the API service is initialized
- **THEN** an Axios instance is created with base URL from `VITE_LOGGER_API_BASE_URL`

#### Scenario: Environment variable usage
- **WHEN** the Axios instance is created
- **THEN** it reads the base URL from `import.meta.env.VITE_LOGGER_API_BASE_URL`

### Requirement: Development workflow
The system SHALL provide clear documentation for running the frontend locally.

#### Scenario: Installation
- **WHEN** developer follows README instructions
- **THEN** `npm install` completes successfully

#### Scenario: Development server
- **WHEN** developer runs `npm run dev`
- **THEN** the frontend development server starts on the configured port

#### Scenario: Production build
- **WHEN** developer runs `npm run build`
- **THEN** a production build is generated without errors

