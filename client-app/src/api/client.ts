import axios from 'axios';
import { useAuthStore } from '../stores/authStore';

const client = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

client.interceptors.request.use((config) => {
  // Respect explicit Authorization headers set by endpoint helpers.
  if (!config.headers.Authorization) {
    const token = useAuthStore.getState().token || useAuthStore.getState().adminToken;
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      console.error('Unauthorized');
    }
    return Promise.reject(error);
  }
);

export default client;
