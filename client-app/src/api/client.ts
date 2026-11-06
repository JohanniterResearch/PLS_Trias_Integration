import axios from 'axios';
import type { RejectedChange } from './changeMetadata';
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
  (response) => {
    notifyRejected(response.data?.rejectedChanges);
    return response;
  },
  (error) => {
    notifyRejected(error.response?.data?.rejectedChanges);
    if (error.response?.status === 401) {
      console.error('Unauthorized');
    }
    return Promise.reject(error);
  }
);

export default client;

function notifyRejected(changes?: RejectedChange[]) {
  if (changes?.length) window.dispatchEvent(new CustomEvent('pls:rejected-changes', { detail: changes }));
}
