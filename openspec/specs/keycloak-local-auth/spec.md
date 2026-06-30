# keycloak-local-auth Specification

## Purpose
TBD - created by archiving change add-keycloak-local-auth-integration. Update Purpose after archive.
## Requirements
### Requirement: Local Keycloak identity provider for development

The project SHALL provide a local Keycloak instance via Docker Compose for development-time JWT token generation and validation.

#### Scenario: Keycloak container starts in docker-compose

- **GIVEN** Docker Compose is running
- **WHEN** `docker compose up -d` is executed
- **THEN** a Keycloak container SHALL start on port 8081 (host)

#### Scenario: Keycloak uses skysim realm

- **GIVEN** Keycloak is running
- **WHEN** the Keycloak admin console is accessed
- **THEN** a realm named `skysim` SHALL be available

#### Scenario: Keycloak exposes skysim-logger-api client

- **GIVEN** Keycloak is running with skysim realm
- **WHEN** the realm clients are examined
- **THEN** a client named `skysim-logger-api` SHALL exist
- **AND** the client SHALL have `Direct Access Grants Enabled` set to true

#### Scenario: Keycloak has logger_admin user

- **GIVEN** Keycloak is running with skysim realm
- **WHEN** the realm users are examined
- **THEN** a user named `logger_admin` SHALL exist
- **AND** the user SHALL have password `admin123`

#### Scenario: Keycloak issues JWT access tokens

- **GIVEN** Keycloak is running with skysim realm and logger_admin user
- **WHEN** a POST request is sent to `http://localhost:8081/realms/skysim/protocol/openid-connect/token` with grant_type=password and valid credentials
- **THEN** Keycloak SHALL return a JSON response containing `access_token`
- **AND** the access_token SHALL be a valid JWT

### Requirement: Logger.Api validates tokens from local Keycloak

Logger.Api SHALL configure JWT Bearer authentication to validate tokens from the local Keycloak instance.

#### Scenario: JWT Authority points to local Keycloak

- **GIVEN** Logger.Api is running in Development
- **WHEN** the JWT configuration is loaded from appsettings.Development.json
- **THEN** the Authority SHALL be `http://localhost:8081/realms/skysim`

#### Scenario: JWT Audience matches Keycloak client

- **GIVEN** Logger.Api is running in Development
- **WHEN** the JWT configuration is loaded from appsettings.Development.json
- **THEN** the Audience SHALL be `skysim-logger-api`

#### Scenario: RequireHttpsMetadata is disabled in Development

- **GIVEN** Logger.Api is running in Development
- **WHEN** the JWT configuration is loaded from appsettings.Development.json
- **THEN** RequireHttpsMetadata SHALL be false

### Requirement: Protected endpoints accept Keycloak tokens

Logger.Api protected endpoints SHALL accept valid JWT tokens issued by local Keycloak.

#### Scenario: Keycloak access tokens include correct audience

- **GIVEN** Keycloak is running with skysim realm
- **WHEN** an access token is issued to `logger_admin`
- **THEN** the token's `aud` claim SHALL include `skysim-logger-api`
- **AND** the token's `iss` claim SHALL be `http://localhost:8081/realms/skysim`

#### Scenario: Token without correct audience is rejected by Logger.Api

- **GIVEN** Logger.Api is running in Development with Audience `skysim-logger-api`
- **WHEN** a request is sent to a protected endpoint with a token whose `aud` does not include `skysim-logger-api`
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Protected endpoint returns 200 with valid token

- **GIVEN** a valid Keycloak access token is obtained
- **WHEN** a request is sent to a protected endpoint with `Authorization: Bearer <token>` header
- **THEN** Logger.Api SHALL return 200 OK

#### Scenario: Protected endpoint returns 401 without token

- **WHEN** a request is sent to a protected endpoint without Authorization header
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Protected endpoint returns 401 with invalid token

- **WHEN** a request is sent to a protected endpoint with an invalid or expired token
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Health endpoint remains public

- **GIVEN** Logger.Api is running
- **WHEN** a request is sent to the health endpoint without Authorization header
- **THEN** Logger.Api SHALL return 200 OK without requiring JWT

### Requirement: Swagger Authorize works with Keycloak tokens

Swagger UI SHALL support entering Bearer tokens for testing protected endpoints.

#### Scenario: Swagger displays Bearer authorization option

- **WHEN** Swagger UI is opened in Development
- **THEN** the Authorize button SHALL be available
- **AND** it SHALL accept Bearer token input

#### Scenario: Authenticated requests work from Swagger

- **GIVEN** a valid Keycloak access token is entered in Swagger Authorize
- **WHEN** a protected endpoint is called from Swagger
- **THEN** the request SHALL include the Bearer token in Authorization header
- **AND** Logger.Api SHALL return 200 OK

### Requirement: Kafka consumer remains unaffected

The Kafka background consumer SHALL continue operating without requiring JWT authentication.

#### Scenario: Kafka consumer starts automatically

- **GIVEN** Logger.Api starts
- **WHEN** the application is running
- **THEN** the Kafka consumer SHALL start consuming messages from skysim.action.logs topic
- **AND** it SHALL NOT require HTTP authentication

