# Implementation Tasks

## 1. Infrastructure Changes

- [x] 1.1 Add Keycloak service to `infra/docker-compose.yml` with port 8081 mapping
- [x] 1.2 Add Keycloak health check configuration to docker-compose
- [x] 1.3 Add depends_on condition for Keycloak if needed
- [x] 1.4 Create `infra/keycloak/skysim-realm.json` for realm import configuration (realm `skysim`, client `skysim-logger-api`, user `logger_admin/admin123`)

## 2. Keycloak Realm Configuration

- [x] 2.1 Configure `skysim` realm with appropriate settings
- [x] 2.2 Create `skysim-logger-api` client with:
  - Client ID: `skysim-logger-api`
  - Direct Access Grants Enabled: true
  - Valid Redirect URIs: appropriate for development
  - Web Origins: `http://localhost:*`
  - **Audience mapper**: Add audience mapper to include `skysim-logger-api` in token's `aud` claim
- [x] 2.3 Create `logger_admin` user with password `admin123`
- [x] 2.4 Configure token settings (access token lifetime, etc.)

## 3. Logger.Api JWT Configuration

- [x] 3.1 Update `backend/Skysim.Logger.Api/appsettings.Development.json`:
  - Authority: `http://localhost:8081/realms/skysim`
  - Audience: `skysim-logger-api`
  - RequireHttpsMetadata: `false`

## 4. Documentation

- [x] 4.1 Create `docs/local-keycloak-setup.md` with:
  - Starting Keycloak instructions
  - Obtaining access token using curl
  - Calling protected Logger.Api endpoint with Bearer token
  - Troubleshooting common issues
- [x] 4.2 Create `scripts/get-token.sh` for easy token retrieval
- [x] 4.3 Update `docs/README.md` or main README to reference Keycloak setup

## 5. Verification and Testing

> **Note:** Tasks 5.1-5.13 require running Docker Compose and the application. Run these manually following the instructions in `docs/local-keycloak-setup.md`.
>
> Tasks 5.14-5.16 have been completed: build succeeds, all 162 tests pass, openspec validation passes.

- [ ] 5.1 Verify Keycloak starts correctly on port 8081
- [ ] 5.2 Verify realm import creates `skysim` realm
- [ ] 5.3 Verify `skysim-logger-api` client exists with Direct Access Grants enabled
- [ ] 5.4 Verify audience mapper includes `skysim-logger-api` in token's `aud` claim
- [ ] 5.5 Verify `logger_admin` user exists with password `admin123`
- [ ] 5.6 Verify access token can be obtained for `logger_admin/admin123`
- [ ] 5.7 Verify token contains expected audience (`aud = skysim-logger-api`)
- [ ] 5.8 Verify Logger.Api protected endpoint returns 401 without token
- [ ] 5.9 Verify Logger.Api protected endpoint returns 200 with valid Keycloak token
- [ ] 5.10 Verify Logger.Api returns 401 if token audience does not match `skysim-logger-api`
- [ ] 5.11 Verify Swagger Authorize works with Keycloak token
- [ ] 5.12 Verify health endpoint remains public (200 without token)
- [ ] 5.13 Verify Kafka consumer continues working
- [x] 5.14 Run `dotnet build` to ensure build passes ✓
- [x] 5.15 Run existing tests to ensure no regressions ✓ (162 tests passed)
- [x] 5.16 Run `openspec validate add-keycloak-local-auth-integration --strict` ✓
