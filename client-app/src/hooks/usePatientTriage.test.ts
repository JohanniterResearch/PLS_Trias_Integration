import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { usePatientTriage } from './usePatientTriage';
import { useAuthStore } from '../stores/authStore';
import { TRIAGE_COLOR } from '../types/triageColor';

const updateTriageColorMock = vi.fn().mockResolvedValue(undefined);
const updateRespirationMock = vi.fn().mockResolvedValue(undefined);

vi.mock('../api/endpoints', () => ({
  updateTriageColor: (...args: unknown[]) => updateTriageColorMock(...args),
  updateRespiration: (...args: unknown[]) => updateRespirationMock(...args),
}));

describe('usePatientTriage', () => {
  beforeEach(() => {
    updateTriageColorMock.mockReset();
    updateRespirationMock.mockReset();
    useAuthStore.setState({ token: 'test-token', adminToken: null });
  });

  it('writes triage color immediately without geolocation data', async () => {
    const { result } = renderHook(() => usePatientTriage(24));

    await act(async () => {
      await result.current.setTriageColor(TRIAGE_COLOR.GRUEN, { respiration: true });
    });

    expect(updateTriageColorMock).toHaveBeenCalledWith(
      24,
      { triageColor: TRIAGE_COLOR.GRUEN, respiration: true },
      'test-token'
    );
  });

  it('writes respiration immediately without geolocation data', async () => {
    const { result } = renderHook(() => usePatientTriage(24));

    await act(async () => {
      await result.current.setRespiration(false);
    });

    expect(updateRespirationMock).toHaveBeenCalledWith(
      24,
      { respiration: false },
      'test-token'
    );
  });
});
