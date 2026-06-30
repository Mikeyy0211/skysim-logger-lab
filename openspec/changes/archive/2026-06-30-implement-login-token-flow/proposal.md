## Why

The SkySim Logger Admin frontend needs user authentication to protect sensitive log data. Currently, the static UI pages exist but lack any authentication layer. Implementing a login token flow will secure the admin dashboard and log viewer while preparing the foundation for API integration.

## What Changes

- Create auth service to handle Keycloak token-based authentication
- Create token storage utility for localStorage management
- Update LoginPage with form handling and error states
- Create ProtectedRoute component for route security
- Apply authentication guards to dashboard and log pages
- Update Header logout functionality
- Configure Axios interceptor for Bearer token attachment

## Capabilities

### New Capabilities

- `login-token-flow`: Frontend authentication flow using Keycloak password grant type with local token storage and protected route guards
- `auth-interceptor`: Axios request interceptor to attach Bearer token to API calls

### Modified Capabilities

<!-- No existing spec requirements are being modified -->

## Impact

- **Frontend**: New `authService.ts`, `tokenStorage.ts`, `ProtectedRoute.tsx`; updated `LoginPage.tsx`, `Header.tsx`, `api.ts`
- **Dependencies**: No new dependencies required
- **Configuration**: Uses existing `VITE_KEYCLOAK_*` environment variables
