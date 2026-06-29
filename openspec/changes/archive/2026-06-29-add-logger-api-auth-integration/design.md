# Design: Add JWT Bearer Authentication to Logger.Api

## Context

Logger.Api exposes query APIs via two controllers:

**LogFlowsController** (`api/log-flows`):
- GET /api/log-flows - paginated list with filtering
- GET /api/log-flows/{flowId} - flow details
- GET /api/log-flows/{flowId}/actions - actions for a flow

**LogActionsController** (`api/log-actions`):
- GET /api/log-actions/{actionId}/details - action details with payloads

These endpoints are currently publicly accessible. Additionally:
- `/health` endpoint exists (health checks, should remain public)
- Kafka consumer runs as background hosted service (no HTTP involved)
- Project uses .NET 8, Swashbuckle for Swagger

## Goals / Non-Goals

**Goals:**
- Add JWT Bearer authentication to all existing actions in LogFlowsController
- Add JWT Bearer authentication to all existing actions in LogActionsController
- Return 401 Unauthorized for requests without valid JWT
- Enable Swagger UI to accept Bearer tokens (Authorize button)
- Keep Kafka consumer unaffected
- Keep health endpoint public

**Non-Goals:**
- No Keycloak Docker container
- No login endpoint
- No role-based authorization
- No bypassing auth in Development environment
- No database schema changes
- No modifications to Logger.Client, Logger.Contracts, Logger.Infrastructure, or SampleService

## Decisions

### 1. Use Microsoft.AspNetCore.Authentication.JwtBearer Package

**Decision:** Add `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package.

**Rationale:**
- Built-in ASP.NET Core support, no third-party dependencies
- Easy Swagger integration via Swashbuckle
- Well-documented, suitable for junior developers

### 2. Add [Authorize] at Controller Level

**Decision:** Apply `[Authorize]` attribute at the controller class level for both LogFlowsController and LogActionsController.

**Rationale:**
- Clean and declarative
- Protects all actions automatically
- Easy to add exceptions if needed (e.g., future public endpoints)

### 3. Configure JWT in appsettings.json

**Decision:** Add JWT configuration under "Jwt" section in appsettings.json.

**Configuration structure:**
```json
"Jwt": {
  "Authority": "https://your-auth-server.com",
  "Audience": "logger-api",
  "RequireHttpsMetadata": true
}
```

**Rationale:**
- Follows existing pattern (Kafka, Logger sections)
- Easy to override via environment variables
- Centralized configuration

### 4. Configure Swagger Bearer Support

**Decision:** Configure SwaggerGen with JWT Bearer security definition.

**Implementation:** Use `options.AddSecurityDefinition("Bearer", ...)` and `options.AddSecurityRequirement(...)`.

**Rationale:**
- Enables testing from Swagger UI
- Developer-friendly for local testing

### 5. Middleware Order

**Decision:** Authentication/Authorization middleware after UseSwaggerUI and health endpoint, before other middleware.

**Order:**
1. UseSwagger / UseSwaggerUI (if Development)
2. Health endpoint (remains anonymous)
3. UseAuthentication()
4. UseAuthorization()
5. LoggerMiddleware (existing)
6. MapControllers() (controllers have [Authorize])

**Rationale:**
- Swagger UI remains accessible for testing
- Health endpoint works without auth
- Controllers are protected by [Authorize] attribute

### 6. No Auth Bypass in Development

**Decision:** Do not implement Development-only auth bypass.

**Rationale:**
- Consistent behavior across environments
- Catches auth issues early
- Production-like testing in Development

### 7. Health Endpoint Remains Anonymous

**Decision:** Keep `/health` endpoint accessible without authentication.

**Rationale:**
- Health checks used by load balancers and orchestrators
- Infrastructure probes should not require JWT

## Implementation Checklist

1. Add NuGet package: `Microsoft.AspNetCore.Authentication.JwtBearer`
2. Create JwtOptions configuration class
3. Add Jwt configuration to appsettings.json
4. Register authentication services in Program.cs:
   - Configure<JwtOptions>
   - AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
   - AddJwtBearer(...)
   - AddAuthorization()
5. Add middleware in correct order (after Swagger, before controllers):
   - UseAuthentication()
   - UseAuthorization()
6. Add `[Authorize]` to LogFlowsController class
7. Add `[Authorize]` to LogActionsController class
8. Configure Swagger Bearer security
9. Verify build and tests pass

## Open Questions

None for this phase.

## Testing Notes

For this phase, testing a 200 OK with valid JWT requires a valid token from an external auth server (e.g., Keycloak in a future phase). The following can be verified:

1. Protected endpoints without Authorization header return 401
2. Swagger UI shows Authorize button
3. Existing unit tests pass
4. Build succeeds
