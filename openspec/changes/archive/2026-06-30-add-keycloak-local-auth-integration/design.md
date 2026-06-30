## Context

Phase 5 added JWT Bearer authentication to Logger.Api, protecting query endpoints with 401 responses when no Authorization header is provided. Swagger UI is configured with Bearer token support. However, the `appsettings.Development.json` currently points the JWT Authority to `http://localhost:8080/realms/skysim`, which conflicts with Kafka UI. No local Keycloak instance exists to issue valid tokens.

The current `infra/docker-compose.yml` contains only postgres, kafka, and kafka-ui services. Keycloak needs to be added as a new container service, and Logger.Api's JWT configuration needs to be corrected.

## Goals / Non-Goals

**Goals:**

- Add Keycloak container to docker-compose.yml for local development
- Expose Keycloak on localhost:8081 to avoid conflict with Kafka UI on 8080
- Configure Keycloak with realm `skysim`, client `skysim-logger-api`, and test user `logger_admin/admin123`
- Update Logger.Api JWT settings in appsettings.Development.json to point to the correct Keycloak URL
- Provide documentation and scripts for starting Keycloak and obtaining access tokens via curl
- Keep Logger.Api as a resource server (validates tokens only, does not issue them)
- Ensure Swagger Authorize works with the locally-issued tokens

**Non-Goals:**

- Implementing login endpoint in Logger.Api
- Frontend login implementation
- Role-based authorization
- Modifying Logger.Client
- Modifying Logger.Contracts (unless strictly required)
- Modifying Kafka consumer/producer behavior
- Changing database schema
- Production-hardening the Keycloak configuration

## Decisions

### Decision: Use Keycloak 24.x as the local identity provider

**Rationale:** Keycloak is the standard identity provider used at Skysim. Using the same tool locally ensures parity with production authentication flows. Keycloak provides a complete OAuth2/OIDC implementation with token issuance via its admin API or direct `/protocol/openid-connect/token` endpoint.

**Alternative:** Using a minimal mock JWT issuer (e.g., a simple ASP.NET Core token endpoint) was considered. This was rejected because it would not reflect the real authentication flow and would require different integration code than production Keycloak.

### Decision: Expose Keycloak on port 8081 (host) instead of 8080

**Rationale:** Kafka UI currently uses port 8080 for its web interface. Port conflicts would prevent both services from running simultaneously. Keycloak's internal port 8080 is remapped to host port 8081 via Docker port mapping.

**Alternative:** Reconfiguring Kafka UI to a different port was rejected because it would require changes to existing documentation and workflows that other developers may already be using.

### Decision: Use Keycloak's Direct Access Grants for token generation

**Rationale:** The simplest way to obtain a token for testing is to use Keycloak's Resource Owner Password Credentials flow (direct access grants). This allows obtaining a token with a simple POST to the token endpoint using username/password credentials. This approach requires enabling the `Direct Access Grants Enabled` client option in Keycloak.

**Alternative:** Using an admin client to programmatically create users and generate tokens was considered but would add complexity. The direct access grants approach is sufficient for local development testing.

### Decision: Configure Logger.Api JWT settings via appsettings.Development.json

**Rationale:** The existing JWT configuration structure in appsettings.json already supports Authority, Audience, and RequireHttpsMetadata. Setting these values in appsettings.Development.json keeps configuration environment-specific and avoids hardcoding. The current settings in appsettings.Development.json incorrectly point to port 8080, which will be fixed to 8081.

### Decision: Use Keycloak realm import for initial configuration

**Rationale:** Keycloak supports importing a pre-configured realm on startup via a JSON file. This ensures consistent configuration across developer environments and eliminates manual setup steps. The realm import will include the `skysim` realm with the `skysim-logger-api` client and `logger_admin` user.

## Risks / Trade-offs

[Risk] Keycloak startup time may delay local development → Mitigation: Document Keycloak startup in README and ensure docker-compose waits for Keycloak health check before starting dependent services if needed.

[Risk] Keycloak admin console may conflict with Kafka UI if not properly isolated → Mitigation: Explicitly configure Keycloak to use port 8081 on the host, documented clearly in the setup scripts.

[Risk] Token expiration requires re-obtaining tokens frequently → Mitigation: Use long-lived tokens for development (e.g., 1 hour session) or provide scripts to easily refresh tokens.

[Risk] Missing audience claim causes 401 rejection → Mitigation: Configure Keycloak audience mapper to include `skysim-logger-api` in the token's `aud` claim. Without this, Logger.Api will reject the token even if the issuer is correct.

[Risk] Keycloak issuer mismatch with Authority URL → Mitigation: Set `KC_HTTP_PORT` and ensure `KC_HOSTNAME_STRICT_HTTPS` is disabled so Keycloak's OIDC discovery document (`/.well-known/openid-configuration`) reports `issuer: http://localhost:8081/realms/skysim`

## Migration Plan

1. **Update docker-compose.yml**: Add Keycloak service with port 8081 mapping, health check, KC_HEALTH_URL, and volume for realm import
2. **Create realm import file**: Define `infra/keycloak/skysim-realm.json` with realm `skysim`, client `skysim-logger-api`, and user `logger_admin/admin123`. Include audience mapper to set `aud = skysim-logger-api` on access tokens.
3. **Update appsettings.Development.json**: Set Authority to `http://localhost:8081/realms/skysim`, Audience to `skysim-logger-api`, RequireHttpsMetadata to `false`
4. **Create scripts**: Add `scripts/get-token.sh` to obtain access token via curl
5. **Add documentation**: Create `docs/local-keycloak-setup.md` with step-by-step instructions
6. **Verify**: Start services, verify token audience, call protected endpoint to confirm 200 OK

**Rollback:** If issues arise, revert docker-compose.yml and appsettings.Development.json to previous state. Keycloak configuration is self-contained in Docker.

## Open Questions

1. Should Keycloak health be checked before starting Logger.Api in docker-compose? (Recommended: yes, add depends_on with condition)
2. Should we enable CORS in Keycloak for local frontend development? (Recommended: yes, add development origins)
