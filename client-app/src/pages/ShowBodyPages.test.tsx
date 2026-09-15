import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ShowBodyBack from './ShowBodyBack';
import ShowBodyFront from './ShowBodyFront';

const navigateMock = vi.fn();
const getBodyPartsMock = vi.fn();
const saveBodyPartMock = vi.fn();

vi.mock('../stores/authStore', () => ({
  useAuthStore: () => ({ token: 'token' }),
}));

vi.mock('../api/endpoints', () => ({
  getBodyParts: (...args: unknown[]) => getBodyPartsMock(...args),
  saveBodyPart: (...args: unknown[]) => saveBodyPartMock(...args),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigateMock,
  };
});

function renderAt(path: string, element: ReactElement, state = { patientId: 12, operationSceneId: 77, returnTo: '/TriagePage2', triageResult: 'rot' }) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: path, state }]}>
      <Routes>
        <Route path={path} element={element} />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  cleanup();
  navigateMock.mockReset();
  saveBodyPartMock.mockReset();
  getBodyPartsMock.mockResolvedValue({ data: { kopf_vorne: 0 } });
});

describe('ShowBody pages preserve triage return state', () => {
  it('front page returns to the originating triage page and preserves state when switching views', async () => {
    renderAt('/ShowBodyFront', <ShowBodyFront />);
    await screen.findByRole('heading', { name: /front/i });

    fireEvent.click(screen.getByRole('button', { name: /zurück zur triage/i }));
    expect(navigateMock).toHaveBeenCalledWith('/TriagePage2', {
      state: { patientId: 12, operationSceneId: 77, triageResult: 'rot' },
    });

    navigateMock.mockReset();
    fireEvent.click(screen.getByRole('button', { name: /rückseite anzeigen/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyBack', {
      state: { patientId: 12, operationSceneId: 77, returnTo: '/TriagePage2', triageResult: 'rot' },
    });
  });

  it('back page returns to the originating triage page and preserves state when switching views', async () => {
    renderAt('/ShowBodyBack', <ShowBodyBack />, { patientId: 13, operationSceneId: 78, returnTo: '/TriagePage3', triageResult: 'gelb' });
    await screen.findByRole('heading', { name: /back/i });

    fireEvent.click(screen.getByRole('button', { name: /zurück zur triage/i }));
    expect(navigateMock).toHaveBeenCalledWith('/TriagePage3', {
      state: { patientId: 13, operationSceneId: 78, triageResult: 'gelb' },
    });

    navigateMock.mockReset();
    fireEvent.click(screen.getByRole('button', { name: /vorderseite anzeigen/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 13, operationSceneId: 78, returnTo: '/TriagePage3', triageResult: 'gelb' },
    });
  });
});
