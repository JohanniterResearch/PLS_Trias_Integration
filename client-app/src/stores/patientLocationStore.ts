import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import { updatePatientLocation } from '../api/endpoints';
import { useAuthStore } from './authStore';

export type PatientLocationJobStatus = 'locating' | 'writing' | 'written' | 'error';

export interface PatientLocationCoordinates {
  lat: number;
  lng: number;
}

export interface PatientLocationJob {
  patientId: number;
  status: PatientLocationJobStatus;
  coordinates: PatientLocationCoordinates | null;
  error: string | null;
  updatedAt: string;
}

interface PatientLocationState {
  jobs: Record<number, PatientLocationJob>;
  ensurePatientLocation: (patientId: number | undefined) => Promise<void>;
  clearJobs: () => void;
}

const ACTIVE_JOB_STALE_MS = 30_000;

function getErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  if (typeof error === 'object' && error !== null && 'message' in error && typeof error.message === 'string') {
    return error.message;
  }
  return 'Unbekannter Fehler bei der GPS-Ermittlung';
}

function requestCurrentPosition(): Promise<PatientLocationCoordinates> {
  return new Promise((resolve, reject) => {
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      reject(new Error('Geolocation wird von diesem Browser nicht unterstützt'));
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        });
      },
      (error) => reject(error)
    );
  });
}

function isStaleActiveJob(job: PatientLocationJob): boolean {
  if (job.status !== 'locating' && job.status !== 'writing') return false;
  const updatedAt = Date.parse(job.updatedAt);
  if (Number.isNaN(updatedAt)) return true;
  return Date.now() - updatedAt > ACTIVE_JOB_STALE_MS;
}

function buildJob(
  patientId: number,
  status: PatientLocationJobStatus,
  existing: PatientLocationJob | undefined,
  overrides: Partial<Pick<PatientLocationJob, 'coordinates' | 'error'>> = {}
): PatientLocationJob {
  return {
    patientId,
    status,
    coordinates: overrides.coordinates ?? existing?.coordinates ?? null,
    error: overrides.error ?? null,
    updatedAt: new Date().toISOString(),
  };
}

export const usePatientLocationStore = create<PatientLocationState>()(
  persist(
    (set, get) => ({
      jobs: {},
      ensurePatientLocation: async (patientId) => {
        if (!patientId) return;

        const existing = get().jobs[patientId];
        if (existing && !isStaleActiveJob(existing)) return;

        set((state) => ({
          jobs: {
            ...state.jobs,
            [patientId]: buildJob(patientId, 'locating', existing),
          },
        }));

        try {
          const coordinates = await requestCurrentPosition();

          set((state) => ({
            jobs: {
              ...state.jobs,
              [patientId]: buildJob(patientId, 'writing', state.jobs[patientId], { coordinates }),
            },
          }));

          const token = useAuthStore.getState().token;
          if (!token) throw new Error('Kein Auth-Token verfügbar');

          await updatePatientLocation(patientId, coordinates, token);

          set((state) => ({
            jobs: {
              ...state.jobs,
              [patientId]: buildJob(patientId, 'written', state.jobs[patientId], { coordinates }),
            },
          }));
        } catch (error) {
          set((state) => ({
            jobs: {
              ...state.jobs,
              [patientId]: buildJob(patientId, 'error', state.jobs[patientId], {
                error: getErrorMessage(error),
              }),
            },
          }));
        }
      },
      clearJobs: () => set({ jobs: {} }),
    }),
    {
      name: 'patient-location-storage',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({ jobs: state.jobs }),
    }
  )
);
