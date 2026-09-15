import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface AuthState {
  token: string | null;
  adminToken: string | null;
  setToken: (token: string | null) => void;
  setAdminToken: (token: string | null) => void;
  validateToken: (token: string) => Promise<boolean>;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      adminToken: null,
      setToken: (token) => set({ token }),
      setAdminToken: (adminToken) => set({ adminToken }),
      validateToken: async (token: string) => {
        try {
          const response = await fetch('/api/validate-token', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              Authorization: `Bearer ${token}`,
            },
          });
          return response.ok;
        } catch {
          return false;
        }
      },
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({ token: state.token, adminToken: state.adminToken }),
    }
  )
);
