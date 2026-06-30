# Local Keycloak Setup Guide

This guide helps you set up and use Keycloak for local development authentication with the skysim-logger-lab project.

## Overview

Keycloak is configured as a local identity provider for development-time JWT token generation and validation. This allows you to:

- Obtain valid JWT tokens locally for testing protected endpoints
- Test authentication flows without depending on external environments
- Validate that Swagger UI Authorize functionality works correctly

## Prerequisites

- Docker Desktop or compatible container runtime
- `curl` and `jq` installed (for scripts)

## Quick Start

### 1. Start Keycloak

```bash
cd infra
docker compose up -d keycloak
```

Keycloak will be available at:
- **Admin Console**: http://localhost:8081/admin
- **Token Endpoint**: http://localhost:8081/realms/skysim/protocol/openid-connect/token

### 2. Create the Test User

After Keycloak starts (this may take up to 60 seconds), run the setup script:

```bash
./scripts/setup-keycloak.sh
```

This script:
- Waits for Keycloak to be healthy
- Creates the `logger_admin` user with password `admin123`
- Sets up proper permissions

### 3. Obtain an Access Token

```bash
./scripts/get-token.sh
```

This outputs the raw JWT token. For verbose output with details:

```bash
./scripts/get-token.sh --verbose
```

### 4. Use the Token

```bash
# Get token and use it immediately
curl -H "Authorization: Bearer $(./scripts/get-token.sh)" http://localhost:5000/api/log-flows

# Or export as environment variable
export KEYCLOAK_TOKEN="$(./scripts/get-token.sh)"
curl -H "Authorization: Bearer $KEYCLOAK_TOKEN" http://localhost:5000/api/log-flows
```

### 5. Verify Token Works

```bash
# Without token (should return 401)
curl http://localhost:5000/api/log-flows

# With token (should return 200)
curl -H "Authorization: Bearer $(./scripts/get-token.sh)" http://localhost:5000/api/log-flows
```

## Configuration Details

### Realm: skysim

The `skysim` realm is pre-configured via the realm import file at `infra/keycloak/skysim-realm.json`.

### Client: skysim-logger-api

| Setting | Value |
|---------|-------|
| Client ID | `skysim-logger-api` |
| Protocol | OpenID Connect |
| Public Client | Yes (no client secret required) |
| Direct Access Grants | Enabled |
| Service Accounts | Disabled |

### User Credentials

| Property | Value |
|----------|-------|
| Username | `logger_admin` |
| Password | `admin123` |
| Roles | admin |

### Token Settings

| Setting | Value |
|---------|-------|
| Access Token Lifespan | 1 hour (3600 seconds) |
| Audience | `skysim-logger-api` |
| Issuer | `http://localhost:8081/realms/skysim` |

## Manual Token Request

If you prefer to use curl directly:

```bash
curl -X POST "http://localhost:8081/realms/skysim/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=logger_admin" \
  -d "password=admin123" \
  -d "grant_type=password" \
  -d "client_id=skysim-logger-api"
```

Response:

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI...",
  "expires_in": 3600,
  "refresh_token": "...",
  "token_type": "Bearer"
}
```

## Decoding the JWT

To inspect the token payload:

```bash
# Extract and decode the token (add padding if needed)
TOKEN=$(./scripts/get-token.sh)
echo "$TOKEN" | cut -d'.' -f2 | base64 -d 2>/dev/null | jq .
```

Or use the verbose output and copy the token:

```bash
./scripts/get-token.sh --verbose
# Then decode manually
echo "<token>" | cut -d'.' -f2 | base64 -d | jq .
```

## Swagger UI Integration

1. Start Logger.Api in Development mode
2. Open Swagger UI at http://localhost:5000/swagger
3. Click the **Authorize** button
4. Paste your JWT token in the `Bearer <token>` format
5. Click Authorize
6. Now you can test protected endpoints directly from Swagger

## Troubleshooting

### Keycloak doesn't start

Check Docker is running and containers have enough resources:
```bash
docker ps
docker logs skysim-keycloak
```

### Setup script fails with "Failed to obtain admin token"

Wait longer for Keycloak to fully start:
```bash
# Check health
curl http://localhost:8081/health/ready
```

### User creation fails

The user might already exist. Run the setup script again:
```bash
./scripts/setup-keycloak.sh
```

### Token audience mismatch (401 even with valid token)

Ensure the `skysim-logger-api` client has the audience mapper configured. The token's `aud` claim must include `skysim-logger-api`.

### Health endpoint returns 401

The health endpoint should be public. Check your JWT configuration in `appsettings.Development.json` allows anonymous access to `/health`.

### Client configuration not working

If you've already started Keycloak with old configuration and need to apply changes:

```bash
cd infra
docker compose down
docker compose up -d keycloak
./scripts/setup-keycloak.sh
```

> **Note:** You must restart Keycloak to apply realm configuration changes because the realm is imported only on first startup.

## Stopping Keycloak

```bash
cd infra
docker compose down keycloak
```

Or stop everything:

```bash
cd infra
docker compose down
```

## Resetting Keycloak Data

To start fresh (removes all users and configuration):

```bash
cd infra
docker compose down -v
docker compose up -d keycloak
./scripts/setup-keycloak.sh
```

## Related Documentation

- [Logger API Documentation](./04-logger-database-api-design.md)
- [Kafka Message Consumer](./03-kafka-message-consumer-design.md)
- [Smoke Test](./smoke-test.md)
