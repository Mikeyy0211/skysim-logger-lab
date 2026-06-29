# Proposal: Add JWT Bearer Authentication to Logger.Api

## Why

Logger.Api currently exposes query APIs without any authentication. These endpoints provide access to operational log data and should not be publicly accessible. This change adds JWT Bearer authentication to protect all query endpoints in LogFlowsController and LogActionsController while keeping the implementation simple and suitable for junior-level work.

## What Changes

1. Add JWT Bearer authentication middleware to Skysim.Logger.Api
2. Add `[Authorize]` attribute to LogFlowsController (all existing actions)
3. Add `[Authorize]` attribute to LogActionsController (all existing actions)
4. Add JWT configuration to appsettings.json (Authority, Audience, RequireHttpsMetadata)
5. Configure Swagger UI with Bearer authentication support (Authorize button)
6. Keep Kafka consumer and health endpoint unaffected

## Capabilities

### New Capabilities

- `logger-api-auth`: JWT Bearer authentication for Logger.Api query endpoints
  - Adds JWT Bearer authentication to all existing query controller actions
  - Protects LogFlowsController and LogActionsController endpoints
  - Enables Swagger Bearer token input for testing

### Modified Capabilities

- `logger-api-responsibilities`: Query endpoints now require authentication
  - All existing actions in LogFlowsController and LogActionsController return 401 when no valid JWT is provided

## Impact

### Affected Code

- **Skysim.Logger.Api/Program.cs**: JWT middleware registration, Swagger Bearer configuration
- **Skysim.Logger.Api/Controllers/LogFlowsController.cs**: Add [Authorize] attribute
- **Skysim.Logger.Api/Controllers/LogActionsController.cs**: Add [Authorize] attribute
- **Skysim.Logger.Api/appsettings.json**: Add Jwt configuration section

### Not Affected

- Kafka consumer service (background processing)
- Health endpoint `/health`
- Skysim.Logger.Client
- Skysim.Logger.Contracts
- Skysim.Logger.Infrastructure
- Database schema
- API response formats

### Dependencies

- Microsoft.AspNetCore.Authentication.JwtBearer package
- Swashbuckle.AspNetCore (already present for Swagger)

## Testing Notes

Testing a 200 OK response with a valid JWT requires obtaining a valid token from an external auth server such as Keycloak (when available in a future phase). For this phase:

- Protected endpoints without Authorization header must return 401
- Swagger UI must show Authorize button for Bearer token input
- Existing unit tests must continue passing
- Build must pass
