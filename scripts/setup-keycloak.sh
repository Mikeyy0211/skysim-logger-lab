#!/bin/bash
# Keycloak Setup Script
# Creates the logger_admin user in the skysim realm after Keycloak is running

set -e

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8081}"
ADMIN_USER="${KEYCLOAK_ADMIN:-admin}"
ADMIN_PASSWORD="${KEYCLOAK_ADMIN_PASSWORD:-admin}"
REALM="skysim"
USER="logger_admin"
PASSWORD="admin123"

echo "Waiting for Keycloak to be ready..."
until curl -sf "${KEYCLOAK_URL}/health/ready" > /dev/null 2>&1; do
    echo "Keycloak is not ready yet, waiting..."
    sleep 5
done

echo "Keycloak is ready!"

# Get admin token
echo "Obtaining admin token..."
ADMIN_TOKEN=$(curl -s -X POST "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "username=${ADMIN_USER}" \
    -d "password=${ADMIN_PASSWORD}" \
    -d "grant_type=password" \
    -d "client_id=admin-cli" | jq -r '.access_token')

if [ -z "$ADMIN_TOKEN" ] || [ "$ADMIN_TOKEN" == "null" ]; then
    echo "Failed to obtain admin token"
    exit 1
fi

echo "Checking if logger_admin user already exists..."
USER_ID=$(curl -s -X GET "${KEYCLOAK_URL}/admin/realms/${REALM}/users?username=${USER}" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" | jq -r '.[0].id')

if [ -n "$USER_ID" ] && [ "$USER_ID" != "null" ]; then
    echo "User ${USER} already exists (ID: ${USER_ID})"
else
    echo "Creating user ${USER}..."
    USER_ID=$(curl -s -X POST "${KEYCLOAK_URL}/admin/realms/${REALM}/users" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "Content-Type: application/json" \
        -d "{
            \"username\": \"${USER}\",
            \"enabled\": true,
            \"emailVerified\": true,
            \"firstName\": \"Logger\",
            \"lastName\": \"Admin\",
            \"email\": \"logger_admin@skysim.local\"
        }" \
        -D - 2>&1 | grep -i "Location:" | awk '{print $2}' | tr -d '\r')

    # Extract user ID from location header
    USER_ID=$(echo "$USER_ID" | sed 's/.*\///g')
    echo "Created user with ID: ${USER_ID}"
fi

# Set user password
echo "Setting password for ${USER}..."
curl -s -X PUT "${KEYCLOAK_URL}/admin/realms/${REALM}/users/${USER_ID}/reset-password" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{
        \"type\": \"password\",
        \"value\": \"${PASSWORD}\",
        \"temporary\": false
    }"

echo ""
echo "========================================"
echo "Keycloak setup complete!"
echo "========================================"
echo "Realm: ${REALM}"
echo "User: ${USER}"
echo "Password: ${PASSWORD}"
echo ""
echo "Admin Console: ${KEYCLOAK_URL}/admin"
echo "Token Endpoint: ${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token"
echo ""
