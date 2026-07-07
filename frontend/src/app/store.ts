import { configureStore } from '@reduxjs/toolkit';
import { authReducer } from '../features/auth/authSlice';
import { logFilterReducer } from '../features/logs/logFilterSlice';

export const store = configureStore({
  reducer: {
    auth: authReducer,
    logFilters: logFilterReducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
