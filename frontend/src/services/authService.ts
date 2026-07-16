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
  accessToken?: string;
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
      return {
        success: false,
        error: 'Tên đăng nhập hoặc mật khẩu không đúng',
      };
    }

    const data: KeycloakTokenResponse = await response.json();
    if (!data.access_token) {
      return {
        success: false,
        error: 'Không thể đăng nhập. Vui lòng thử lại.',
      };
    }

    setToken(data.access_token);

    return { success: true, accessToken: data.access_token };
  } catch {
    return {
      success: false,
      error: 'Không thể kết nối đến máy chủ xác thực.',
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
