## Context

The SkySim Logger Admin frontend currently has static UI pages without authentication. The backend Keycloak is configured and running locally at `http://localhost:8081` with realm `skysim`. The frontend uses React + TypeScript + Vite with React Router already set up.

## Goals / Non-Goals

**Goals:**
- Implement password grant flow with Keycloak for frontend authentication
- Store access token securely in localStorage
- Protect dashboard, logs, and log detail routes
- Add logout functionality with token cleanup
- Configure Axios interceptor for Bearer token attachment

**Non-Goals:**
- Backend changes
- Docker configuration changes
- Redux Toolkit state management
- Refresh token rotation
- Role-based access control
- Keycloak JS adapter usage
- Redirect-based OAuth flow
- Logger API data loading

## Decisions

### 1. Token Storage Approach
**Decision:** Use localStorage with a namespaced key
**Rationale:** Simple, works across page refreshes without session storage complexity. Key `skysim_logger_access_token` prevents collisions with other apps.

### 2. Authentication State Check
**Decision:** Check `isAuthenticated()` by verifying token existence (not expiration validation)
**Rationale:** Keep implementation simple. Full JWT validation can be added later if needed. Token presence is sufficient for this phase.

### 3. HTTP Client for Token Request
**Decision:** Use fetch API for Keycloak token endpoint
**Rationale:** Simple POST with form-urlencoded body. Fetch is native and doesn't require additional axios setup for just this call.

### 4. Route Protection Strategy
**Decision:** Create `ProtectedRoute` wrapper component
**Rationale:** Standard React Router pattern. Cleaner than modifying each page component. Easy to extend with loading states later.

### 5. Error Handling
**Decision:** Display user-friendly inline error messages on login page
**Rationale:** Simple UX. Show "Login failed. Please check your username and password." for auth errors and "Unable to connect to authentication server." for network issues.

## File Structure

```
frontend/src/
├── services/
│   ├── api.ts              (update - add interceptor)
│   └── authService.ts      (new)
├── utils/
│   └── tokenStorage.ts     (new)
├── components/
│   └── ProtectedRoute.tsx  (new)
├── layouts/
│   └── AdminLayout.tsx     (update - add logout)
└── pages/
    └── LoginPage.tsx       (update - add form handling)
```

## API Integration

### Keycloak Token Endpoint
- **URL:** `POST ${VITE_KEYCLOAK_BASE_URL}/realms/${VITE_KEYCLOAK_REALM}/protocol/openid-connect/token`
- **Content-Type:** `application/x-www-form-urlencoded`
- **Body:**
  ```
  client_id=skysim-logger-api
  username=<username>
  password=<password>
  grant_type=password
  ```
- **Success Response:** `{ access_token: "...", token_type: "Bearer", ... }`

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Token stored in localStorage is vulnerable to XSS | Acceptable for internal admin tool; HTTPS required in production |
| No token expiration validation | Token presence check only; Keycloak handles expiration |
| No refresh token rotation | Single-page session only; user re-logs in after token expires |
| CORS issues with Keycloak | Keycloak must allow frontend origin; configured in Keycloak client |

## Open Questions

- Should we add a token expiration check on app initialization? (Deferred to future)
- Do we need a loading state during initial auth check? (Deferred to future)
