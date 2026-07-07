import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { getToken } from '../../utils/tokenStorage';

export interface AuthUser {
  username: string;
}

interface AuthState {
  accessToken: string | null;
  user: AuthUser | null;
  isAuthenticated: boolean;
}

const storedToken = getToken();

const initialState: AuthState = {
  accessToken: storedToken,
  user: null,
  isAuthenticated: storedToken !== null,
};

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setCredentials(state, action: PayloadAction<{ accessToken: string; username: string }>) {
      state.accessToken = action.payload.accessToken;
      state.user = { username: action.payload.username };
      state.isAuthenticated = true;
    },
    clearCredentials(state) {
      state.accessToken = null;
      state.user = null;
      state.isAuthenticated = false;
    },
    hydrateFromStorage(state) {
      const token = getToken();
      if (token) {
        state.accessToken = token;
        state.isAuthenticated = true;
      }
    },
  },
});

export const { setCredentials, clearCredentials, hydrateFromStorage } = authSlice.actions;
export const { reducer: authReducer } = authSlice;
