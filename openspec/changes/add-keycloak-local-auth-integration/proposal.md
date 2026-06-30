# Proposal: Add Keycloak Local Auth Integration

## Why

Phase 5 added JWT Bearer authentication to Logger.Api query endpoints. Protected endpoints now return 401 without Authorization header, and Swagger supports Bearer token input. However, there is currently no local token issuer, so 200 OK with a valid JWT cannot be tested locally. This change adds local Keycloak infrastructure so developers can obtain valid JWT tokens for testing without relying on external environments.

## What Changes

- Add Keycloak container to `infra/docker-compose.yml` for local development
- Expose Keycloak on localhost:8081 (Kafka UI already uses 8080)
- Configure Keycloak with realm `skysim`, client `skysim-logger-api`, and user `logger_admin/admin123`
- Update `Logger.Api` JWT configuration in `appsettings.Development.json` to validate tokens from local Keycloak
- Add scripts and documentation for starting Keycloak and obtaining access tokens

## Capabilities

### New Capabilities

- `keycloak-local-auth`: Local Keycloak identity provider integration for development-time JWT token generation and validation

### Modified Capabilities

- `logger-api-auth`: Extend JWT validation requirements to include local Keycloak as a token issuer for development

## Impact

- **Docker Compose**: New Keycloak service added; requires Docker Desktop or compatible container runtime
- **Logger.Api**: JWT Bearer authentication updated to point to local Keycloak Authority and Audience
- **Documentation**: New local development guide for starting Keycloak and using curl to obtain tokens
- **No breaking changes**: Existing tests, database schema, and API contracts remain unchanged
