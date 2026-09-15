import type { ReactElement } from 'react';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import TriagePage1 from './TriagePage1';
import TriagePage2 from './TriagePage2';
import TriagePage3 from './TriagePage3';
import { TRIAGE_COLOR } from '../types/triageColor';

const setTriageColorMock = vi.fn();
const setRespirationMock = vi.fn().mockResolvedValue(undefined);
const navigateMock = vi.fn();
const ensurePatientLocationMock = vi.fn().mockResolvedValue(undefined);
const clearJobsMock = vi.fn();
const patientLocationStoreState = {
  jobs: {},
  ensurePatientLocation: ensurePatientLocationMock,
  clearJobs: clearJobsMock,
};

vi.mock('../hooks/usePatientTriage', () => ({
  usePatientTriage: () => ({
    setTriageColor: setTriageColorMock,
    setRespiration: setRespirationMock,
  }),
}));

vi.mock('../stores/patientLocationStore', () => ({
  usePatientLocationStore: (selector: (state: typeof patientLocationStoreState) => unknown) =>
    selector(patientLocationStoreState),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigateMock,
  };
});

function renderAt(path: string, element: ReactElement, state: Record<string, unknown> = { patientId: 12, operationSceneId: 77 }) {
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
  setTriageColorMock.mockReset();
  setRespirationMock.mockReset();
  navigateMock.mockReset();
  ensurePatientLocationMock.mockReset();
});

describe('Triage pages use shared triage contract', () => {
  it('TriagePage1 assigns canonical green and black values', () => {
    renderAt('/TriagePage1', <TriagePage1 />);

    expect(ensurePatientLocationMock).toHaveBeenCalledWith(12);

    fireEvent.click(screen.getByRole('button', { name: /patient gehfähig/i }));
    expect(setTriageColorMock).toHaveBeenCalledWith(TRIAGE_COLOR.GRUEN);

    setTriageColorMock.mockReset();
    renderAt('/TriagePage1', <TriagePage1 />, { patientId: 13, operationSceneId: 77 });
    fireEvent.click(screen.getByRole('button', { name: /eindeutiges todeszeichen/i }));
    expect(setTriageColorMock).toHaveBeenCalledWith(TRIAGE_COLOR.SCHWARZ);
  });

  it('TriagePage2 assigns canonical red value', () => {
    renderAt('/TriagePage2', <TriagePage2 />);

    expect(ensurePatientLocationMock).toHaveBeenCalledWith(12);

    fireEvent.click(screen.getByRole('button', { name: /atmung nicht suffizient/i }));
    expect(setTriageColorMock).toHaveBeenCalledWith(TRIAGE_COLOR.ROT, { respiration: false });
  });

  it('TriagePage3 assigns canonical red and yellow values', () => {
    renderAt('/TriagePage3', <TriagePage3 />);

    expect(ensurePatientLocationMock).toHaveBeenCalledWith(12);

    fireEvent.click(screen.getByRole('button', { name: /blutung nicht stillbar/i }));
    expect(setTriageColorMock).toHaveBeenCalledWith(TRIAGE_COLOR.ROT, { blutung: false });

    setTriageColorMock.mockReset();
    renderAt('/TriagePage3', <TriagePage3 />, { patientId: 14, operationSceneId: 77 });
    fireEvent.click(screen.getByRole('button', { name: /blutung stillbar/i }));
    expect(setTriageColorMock).toHaveBeenCalledWith(TRIAGE_COLOR.GELB, { blutung: true });
  });

  it('passes the current triage page as body return target', () => {
    renderAt('/TriagePage1', <TriagePage1 />, { patientId: 21, operationSceneId: 88 });
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 21, operationSceneId: 88, returnTo: '/TriagePage1', triageResult: undefined },
    });

    cleanup();
    navigateMock.mockReset();
    renderAt('/TriagePage2', <TriagePage2 />, { patientId: 22, operationSceneId: 89 });
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 22, operationSceneId: 89, returnTo: '/TriagePage2', triageResult: undefined },
    });

    cleanup();
    navigateMock.mockReset();
    renderAt('/TriagePage3', <TriagePage3 />, { patientId: 23, operationSceneId: 90 });
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 23, operationSceneId: 90, returnTo: '/TriagePage3', triageResult: undefined },
    });
  });

  it('passes completed triage result to body pages', () => {
    renderAt('/TriagePage1', <TriagePage1 />, { patientId: 31, operationSceneId: 91 });
    fireEvent.click(screen.getByRole('button', { name: /patient gehfähig/i }));
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 31, operationSceneId: 91, returnTo: '/TriagePage1', triageResult: TRIAGE_COLOR.GRUEN },
    });

    cleanup();
    navigateMock.mockReset();
    renderAt('/TriagePage2', <TriagePage2 />, { patientId: 32, operationSceneId: 92 });
    fireEvent.click(screen.getByRole('button', { name: /atmung nicht suffizient/i }));
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 32, operationSceneId: 92, returnTo: '/TriagePage2', triageResult: TRIAGE_COLOR.ROT },
    });

    cleanup();
    navigateMock.mockReset();
    renderAt('/TriagePage3', <TriagePage3 />, { patientId: 33, operationSceneId: 93 });
    fireEvent.click(screen.getByRole('button', { name: /blutung stillbar/i }));
    fireEvent.click(screen.getByRole('button', { name: /körper markieren/i }));
    expect(navigateMock).toHaveBeenCalledWith('/ShowBodyFront', {
      state: { patientId: 33, operationSceneId: 93, returnTo: '/TriagePage3', triageResult: TRIAGE_COLOR.GELB },
    });
  });

  it('restores completed triage result from route state', () => {
    renderAt('/TriagePage1', <TriagePage1 />, { patientId: 41, operationSceneId: 94, triageResult: TRIAGE_COLOR.SCHWARZ });
    expect(screen.getByText(/kategorie schwarz/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /patient gehfähig/i })).not.toBeInTheDocument();

    cleanup();
    renderAt('/TriagePage2', <TriagePage2 />, { patientId: 42, operationSceneId: 95, triageResult: TRIAGE_COLOR.ROT });
    expect(screen.getByText(/kategorie rot/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /atmung suffizient/i })).not.toBeInTheDocument();

    cleanup();
    renderAt('/TriagePage3', <TriagePage3 />, { patientId: 43, operationSceneId: 96, triageResult: TRIAGE_COLOR.GELB });
    expect(screen.getByText(/kategorie gelb/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /blutung stillbar/i })).not.toBeInTheDocument();
  });
});
