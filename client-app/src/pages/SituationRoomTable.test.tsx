import type { ReactNode } from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, expect, it, vi } from 'vitest';
import SituationRoomTable from './SituationRoomTable';

const navigateMock = vi.fn();

vi.mock('../stores/authStore', () => ({
  useAuthStore: () => ({ token: 'test-token' }),
}));

vi.mock('../api/endpoints', () => ({
  getPersons: vi.fn().mockResolvedValue({
    data: [
      {
        idpatient: 9,
        atmung: true,
        blutung: false,
        radialispuls: null,
        triagefarbe: 'rot',
        transport: true,
        dringend: true,
        kontaminiert: false,
        name: 'Max',
        longitude_patient: 16.4,
        latitude_patient: 48.27,
        user_iduser: null,
        created_at: '2026-03-26T10:00:00.000Z',
        updated_at: '2026-03-26T10:01:00.000Z',
      },
      {
        idpatient: 10,
        atmung: true,
        blutung: false,
        radialispuls: null,
        triagefarbe: 'orange',
        transport: false,
        dringend: false,
        kontaminiert: false,
        name: 'Erika',
        longitude_patient: 16.41,
        latitude_patient: 48.28,
        user_iduser: null,
        created_at: '2026-03-26T10:02:00.000Z',
        updated_at: '2026-03-26T10:03:00.000Z',
      },
    ],
  }),
}));

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  TileLayer: () => null,
  Marker: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Popup: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock('leaflet', () => ({
  default: {
    divIcon: vi.fn(),
  },
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigateMock,
  };
});

beforeEach(() => {
  navigateMock.mockReset();
});

it('navigates to page 1 on click and keyboard activation', async () => {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/SituationRoomTable', state: { operationSceneId: 55 } }]}>
      <Routes>
        <Route path="/SituationRoomTable" element={<SituationRoomTable />} />
      </Routes>
    </MemoryRouter>
  );

  const row = await screen.findByRole('button', { name: /ambulanzprotokoll für patient 9 öffnen/i });
  fireEvent.click(row);
  expect(navigateMock).toHaveBeenCalledWith('/AmbulanzprotokollPage1/9', { state: { operationSceneId: 55 } });

  navigateMock.mockReset();
  fireEvent.keyDown(row, { key: 'Enter' });
  expect(navigateMock).toHaveBeenCalledWith('/AmbulanzprotokollPage1/9', { state: { operationSceneId: 55 } });
});

it('renders invalid triage values explicitly', async () => {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/SituationRoomTable', state: { operationSceneId: 55 } }]}>
      <Routes>
        <Route path="/SituationRoomTable" element={<SituationRoomTable />} />
      </Routes>
    </MemoryRouter>
  );

  expect(await screen.findByText('Ungültig: orange')).toBeInTheDocument();
  expect(screen.getAllByText('1 Ungültig').length).toBeGreaterThan(0);
});
