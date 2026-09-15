import { createBrowserRouter } from 'react-router-dom';
import LandingPage from './pages/LandingPage';
import QRAuthenticator from './pages/QRAuthenticator';
import AdminLandingPage from './pages/AdminLandingPage';
import RoleSelectionPage from './pages/RoleSelectionPage';
import ScanPatient from './pages/ScanPatient';
import TriagePage1 from './pages/TriagePage1';
import TriagePage2 from './pages/TriagePage2';
import TriagePage3 from './pages/TriagePage3';
import AmbulanzprotokollPage1 from './pages/AmbulanzprotokollPage1';
import ShowBodyFront from './pages/ShowBodyFront';
import ShowBodyBack from './pages/ShowBodyBack';
import SituationRoomTable from './pages/SituationRoomTable';
import AdminSettingsPage from './pages/AdminSettingsPage';
import ChangeAdminPasswordPage from './pages/ChangeAdminPasswordPage';
import CreateLoginQrCodesPage from './pages/CreateLoginQrCodesPage';
import ShowUnusedLoginQrCodesPage from './pages/ShowUnusedLoginQrCodesPage';
import CreatePatientQrCodes from './pages/CreatePatientQrCodes';
import ShowUnusedPatientQrCodesPage from './pages/ShowUnusedPatientQrCodesPage';
import EditOperationScene from './pages/EditOperationScene';
import CreateOperationScene from './pages/CreateOperationScene';
import CreateNewAdminUserPage from './pages/CreateNewAdminUserPage';
import NotFound from './pages/NotFound';
import ProtectedRoute from './components/ProtectedRoute';
import ProtectedAdminRoute from './components/ProtectedAdminRoute';

export const router = createBrowserRouter([
  { path: '/', element: <LandingPage /> },
  { path: '/qrAuthenticator', element: <QRAuthenticator /> },
  { path: '/AdminLandingPage', element: <AdminLandingPage /> },

  // Protected (user/QR token)
  { path: '/RoleSelection', element: <ProtectedRoute element={<RoleSelectionPage />} /> },
  { path: '/ScanPatient', element: <ProtectedRoute element={<ScanPatient />} /> },
  { path: '/TriagePage1', element: <ProtectedRoute element={<TriagePage1 />} /> },
  { path: '/TriagePage2', element: <ProtectedRoute element={<TriagePage2 />} /> },
  { path: '/TriagePage3', element: <ProtectedRoute element={<TriagePage3 />} /> },
  { path: '/AmbulanzprotokollPage1/:patientId', element: <ProtectedRoute element={<AmbulanzprotokollPage1 />} /> },
  { path: '/ShowBodyFront', element: <ProtectedRoute element={<ShowBodyFront />} /> },
  { path: '/ShowBodyBack', element: <ProtectedRoute element={<ShowBodyBack />} /> },
  { path: '/SituationRoomTable', element: <ProtectedRoute element={<SituationRoomTable />} /> },

  // Protected (admin token)
  { path: '/AdminSettingsPage', element: <ProtectedAdminRoute element={<AdminSettingsPage />} /> },
  { path: '/ChangeAdminPasswordPage', element: <ProtectedAdminRoute element={<ChangeAdminPasswordPage />} /> },
  { path: '/CreateLoginQrCodesPage', element: <ProtectedAdminRoute element={<CreateLoginQrCodesPage />} /> },
  { path: '/ShowUnusedLoginQrCodesPage', element: <ProtectedAdminRoute element={<ShowUnusedLoginQrCodesPage />} /> },
  { path: '/CreatePatientQrCodePage', element: <ProtectedAdminRoute element={<CreatePatientQrCodes />} /> },
  { path: '/ShowUnusedPatientQrCodesPage', element: <ProtectedAdminRoute element={<ShowUnusedPatientQrCodesPage />} /> },
  { path: '/EditOperationScene', element: <ProtectedAdminRoute element={<EditOperationScene />} /> },
  { path: '/CreateOperationScene', element: <ProtectedAdminRoute element={<CreateOperationScene />} /> },
  { path: '/CreateNewAdminUserPage', element: <ProtectedAdminRoute element={<CreateNewAdminUserPage />} /> },

  { path: '*', element: <NotFound /> },
]);
