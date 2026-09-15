import { useAuthStore } from '../stores/authStore';
import { updateTriageColor, updateRespiration } from '../api/endpoints';
import type { TriageColor } from '../types/triageColor';

export function usePatientTriage(patientId: number | undefined) {
  const { token } = useAuthStore();

  const setTriageColor = async (
    color: TriageColor,
    extra?: { respiration?: boolean; blutung?: boolean }
  ) => {
    if (!patientId) return;
    try {
      await updateTriageColor(
        patientId,
        {
          triageColor: color,
          ...extra,
        },
        token!
      );
    } catch (error) {
      console.error('Fehler beim Aktualisieren der Triagefarbe:', error);
    }
  };

  const setRespiration = async (value: boolean) => {
    if (!patientId) return;
    try {
      await updateRespiration(
        patientId,
        {
          respiration: value,
        },
        token!
      );
    } catch (error) {
      console.error('Fehler beim Aktualisieren der Atmung:', error);
    }
  };

  return { setTriageColor, setRespiration };
}
