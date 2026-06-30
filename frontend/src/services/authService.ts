import { getToken, setToken, removeToken } from '../utils/tokenStorage';

const KEYCLOAK_BASE_URL = import.meta.env.VITE_KEYCLOAK_BASE_URL || 'http://localhost:8081';
const KEYCLOAK_REALM = import.meta.env.VITE_KEYCLOAK_REALM || 'skysim';
const KEYCLOAK_CLIENT_ID = import.meta.env.VITE_KEYCLOAK_CLIENT_ID || 'skysim-logger-api';

interface KeycloakTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  scope?: string;
}

export interface LoginResult {
  success: boolean;
  error?: string;
}

function buildTokenEndpoint(): string {
  return `${KEYCLOAK_BASE_URL}/realms/${KEYCLOAK_REALM}/protocol/openid-connect/token`;
}

export async function login(username: string, password: string): Promise<LoginResult> {
  const params = new URLSearchParams({
    client_id: KEYCLOAK_CLIENT_ID,
    username,
    password,
    grant_type: 'password',
  });

  try {
    const response = await fetch(buildTokenEndpoint(), {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body: params.toString(),
    });

    if (!response.ok) {
      const errorText = await response.text();
      console.error('Keycloak auth error:', response.status, errorText);
      return {
        success: false,
        error: 'Login failed. Please check your username and password.',
      };
    }

    const data: KeycloakTokenResponse = await response.json();
    setToken(data.access_token);

    return { success: true };
  } catch (networkError) {
    console.error('Network error during login:', networkError);
    return {
      success: false,
      error: 'Unable to connect to authentication server.',
    };
  }
}

export function logout(): void {
  removeToken();
}

export function getAccessToken(): string | null {
  return getToken();
}

export function isAuthenticated(): boolean {
  return getToken() !== null;
}
