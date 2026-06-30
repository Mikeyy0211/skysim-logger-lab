#!/bin/bash
# Get Keycloak Access Token for skysim-logger-api
# Usage:
#   ./get-token.sh                    # Output raw token only
#   ./get-token.sh --verbose          # Output detailed info
#   ./get-token.sh [username] [password]

set -e

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8081}"
REALM="skysim"
CLIENT="skysim-logger-api"
VERBOSE=false

# Parse arguments
if [ "$1" = "--verbose" ] || [ "$1" = "-v" ]; then
    VERBOSE=true
    shift
fi

USERNAME="${1:-logger_admin}"
PASSWORD="${2:-admin123}"

TOKEN_RESPONSE=$(curl -s -X POST "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "username=${USERNAME}" \
    -d "password=${PASSWORD}" \
    -d "grant_type=password" \
    -d "client_id=${CLIENT}")

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')

if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" == "null" ]; then
    if [ "$VERBOSE" = true ]; then
        echo "Failed to obtain access token!" >&2
        echo "Response: $TOKEN_RESPONSE" >&2
    fi
    exit 1
fi

if [ "$VERBOSE" = true ]; then
    REFRESH_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.refresh_token')
    TOKEN_TYPE=$(echo "$TOKEN_RESPONSE" | jq -r '.token_type')
    EXPIRES_IN=$(echo "$TOKEN_RESPONSE" | jq -r '.expires_in')

    echo "========================================"
    echo "Access token obtained successfully!"
    echo "========================================"
    echo "Token Type: ${TOKEN_TYPE}"
    echo "Expires In: ${EXPIRES_IN} seconds"
    echo ""
    echo "Access Token:"
    echo "$ACCESS_TOKEN"
    echo ""
    echo "========================================"
    echo "Use with curl:"
    echo "========================================"
    echo "curl -H \"Authorization: Bearer ${ACCESS_TOKEN}\" ..."
    echo ""
    echo "Or export as environment variable:"
    echo "export KEYCLOAK_TOKEN=\"${ACCESS_TOKEN}\""
else
    echo "$ACCESS_TOKEN"
fi
