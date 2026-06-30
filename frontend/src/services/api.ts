import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_LOGGER_API_BASE_URL || 'http://localhost:5108';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);
