# logger-api-auth Specification

## Purpose
TBD - created by archiving change add-logger-api-auth-integration. Update Purpose after archive.
## Requirements
### Requirement: Logger API query endpoints require JWT authentication

All existing actions in LogFlowsController and LogActionsController SHALL require JWT Bearer authentication.

#### Scenario: Request without Authorization header is rejected

- **WHEN** a client calls any existing LogFlowsController or LogActionsController endpoint without an Authorization header
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Swagger exposes Bearer token authorization

- **WHEN** Swagger UI is opened in Development
- **THEN** Swagger UI SHALL provide a Bearer token Authorize option

#### Scenario: Health endpoint remains public

- **GIVEN** a health endpoint exists
- **WHEN** a client calls the health endpoint without Authorization header
- **THEN** Logger.Api SHALL allow the request without requiring JWT

#### Scenario: Kafka background consumer is unaffected

- **WHEN** Logger.Api starts
- **THEN** the Kafka consumer SHALL continue running as a background service
- **AND** it SHALL NOT require HTTP authentication

### Requirement: Logger API validates JWT Bearer tokens using configuration

Logger.Api SHALL configure JWT Bearer authentication using Authority, Audience, and RequireHttpsMetadata values from appsettings.

#### Scenario: JWT configuration exists

- **WHEN** Logger.Api starts
- **THEN** JWT Bearer authentication SHALL be configured from the Jwt configuration section

#### Scenario: Valid token testing requires external auth server

- **GIVEN** this phase does not provide Keycloak, login, or token generation
- **THEN** successful 200 OK testing with a valid JWT SHALL require a token from an external or future local auth server

