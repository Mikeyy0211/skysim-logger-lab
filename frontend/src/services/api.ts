import axios from 'axios';
import { getToken, removeToken } from '../utils/tokenStorage';

const API_BASE_URL = import.meta.env.VITE_LOGGER_API_BASE_URL || 'http://localhost:5108';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(
  (config) => {
    const token = getToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && window.location.pathname !== '/login') {
      removeToken();
      window.location.href = '/login';
    } else {
      console.error('API Error:', error);
    }
    return Promise.reject(error);
  }
);
