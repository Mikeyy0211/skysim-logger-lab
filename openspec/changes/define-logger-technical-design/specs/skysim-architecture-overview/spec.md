# Capability: Skysim Architecture Overview

## ADDED Requirements

### Requirement: Skysim system is decomposed into frontend, gateway, auth, microservices, async messaging, Logger, and database tiers
The Skysim platform SHALL be organized into the following tiers, and the Logger module SHALL sit alongside the business microservices as a centralized, asynchronous consumer of business events.

#### Scenario: Tier breakdown is documented
- **WHEN** the architecture overview is read by a new contributor
- **THEN** they can identify the frontend tier (B2C website, B2B web portal, CMS), the edge/CDN tier (Cloudflare / Nginx), the API gateway tier (KONG), the authentication tier (Keycloak using JWT/OAuth2/OIDC), the microservices tier (Order, Payment, Provider/Core, Notification, plus Logger), the asynchronous messaging tier (Kafka), the cache tier (Redis where applicable), and the data tier (PostgreSQL for the Logger)

#### Scenario: Logger is shown as a downstream consumer of business events
- **WHEN** the diagram is rendered
- **THEN** the Order, Payment, Provider, and Notification services are shown publishing action events to Kafka, and the Logger service is shown consuming from Kafka and writing to PostgreSQL

### Requirement: Inter-service communication uses REST synchronously and Kafka asynchronously
Synchronous inter-service calls SHALL use REST over HTTP; asynchronous events, audit logs, retry processing, and background jobs SHALL flow through Kafka.

#### Scenario: REST is used for direct request/response
- **WHEN** the Order service needs to request payment from the Payment service during checkout
- **THEN** it uses a synchronous REST call

#### Scenario: Kafka is used for cross-service eventing and Logger ingestion
- **WHEN** any business step completes (e.g. ORDER_CREATED, PAYMENT_SUCCESS)
- **THEN** the producing service publishes a log event to Kafka on `skysim.action.logs`, which the Logger service consumes

### Requirement: Authentication is handled centrally by Keycloak
Keycloak SHALL issue and validate JWTs for end users; service-to-service trust is established at the gateway and through internal service identity.

#### Scenario: End-user identity flows through JWT
- **WHEN** an authenticated checkout request reaches a backend service
- **THEN** the service can resolve the user identity from the JWT validated by Keycloak

#### Scenario: Logger API is reachable behind KONG in production
- **WHEN** the ReactJS log viewer calls a Logger API endpoint in production
- **THEN** the request traverses KONG Gateway and is subject to the same JWT enforcement as any other backend route

### Requirement: The Logger module is treated as a first-class backend service in the architecture
The Logger SHALL be deployed as its own service, own its PostgreSQL schema, and own its Kafka consumer group; it SHALL NOT share a database with any business microservice.

#### Scenario: Logger has its own consumer group
- **WHEN** the Logger service starts
- **THEN** it joins the Kafka consumer group `skysim-logger-consumer` for topic `skysim.action.logs`

#### Scenario: Logger owns its PostgreSQL schema
- **WHEN** migrations are applied
- **THEN** they create tables `log_flows`, `log_actions`, `log_action_details` in a schema dedicated to the Logger
