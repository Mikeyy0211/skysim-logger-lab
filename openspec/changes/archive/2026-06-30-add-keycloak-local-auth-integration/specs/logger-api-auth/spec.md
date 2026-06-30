# logger-api-auth Specification

This is a delta specification that modifies the requirements from the base `logger-api-auth` spec created in phase 5.

## MODIFIED Requirements

### Requirement: Logger API query endpoints require JWT authentication

All existing actions in LogFlowsController and LogActionsController SHALL require JWT Bearer authentication. Valid tokens can be obtained from the local Keycloak instance for development testing.

#### Scenario: Request without Authorization header is rejected

- **WHEN** a client calls any existing LogFlowsController or LogActionsController endpoint without an Authorization header
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Swagger exposes Bearer token authorization

- **WHEN** Swagger UI is opened in Development
- **THEN** Swagger UI SHALL provide a Bearer token Authorize option

#### Scenario: Valid token can be obtained from local Keycloak

- **GIVEN** Keycloak is running locally on port 8081
- **WHEN** a developer obtains an access token using curl with logger_admin credentials
- **THEN** the token SHALL be accepted by Logger.Api protected endpoints

#### Scenario: Health endpoint remains public

- **GIVEN** a health endpoint exists
- **WHEN** a client calls the health endpoint without Authorization header
- **THEN** Logger.Api SHALL allow the request without requiring JWT

#### Scenario: Kafka background consumer is unaffected

- **WHEN** Logger.Api starts
- **THEN** the Kafka consumer SHALL continue running as a background service
- **AND** it SHALL NOT require HTTP authentication

### Requirement: Logger API validates JWT Bearer tokens using configuration

Logger.Api SHALL configure JWT Bearer authentication using Authority, Audience, and RequireHttpsMetadata values from appsettings. In Development, these values point to the local Keycloak instance.

#### Scenario: JWT configuration in Development points to local Keycloak

- **WHEN** Logger.Api starts in Development
- **THEN** JWT Bearer authentication SHALL be configured with:
  - Authority: `http://localhost:8081/realms/skysim`
  - Audience: `skysim-logger-api`
  - RequireHttpsMetadata: `false`

#### Scenario: JWT configuration in Production requires HTTPS

- **WHEN** Logger.Api starts in Production
- **THEN** RequireHttpsMetadata SHALL be `true`
- **AND** Authority SHALL point to the production identity provider
