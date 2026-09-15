import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { IoIosLogOut } from 'react-icons/io';
import { FaKey } from 'react-icons/fa';
import Appbar from '../components/Appbar';

export default function AdminSettingsPage() {
  const navigate = useNavigate();
  const { setToken, setAdminToken } = useAuthStore();

  const handleLogout = () => {
    setToken(null);
    setAdminToken(null);
    window.location.reload();
  };

  const handleChangePassword = () => {
    navigate('/ChangeAdminPasswordPage', {
      state: { username: localStorage.getItem('Username') },
    });
  };

  return (
    <>
      <Appbar />
      <div className="page">
        <div className="page-container">
          <div className="btn-row">
            <button onClick={handleLogout} className="btn-action">
              <IoIosLogOut /> Logout
            </button>
            <button onClick={handleChangePassword} className="btn-primary" style={{ width: 'auto' }}>
              <FaKey style={{ marginRight: '6px' }} /> Passwort ändern
            </button>
          </div>

          <div className="settings-section">
            <p className="settings-section-title">Authorizierungs QR-Codes (Einsatzkräfte)</p>
            <button onClick={() => navigate('/CreateLoginQrCodesPage')} className="wide-button">
              QR-Codes für Authorizierung erzeugen
            </button>
            <button onClick={() => navigate('/ShowUnusedLoginQrCodesPage')} className="wide-button">
              Nicht verwendete QR-Codes anzeigen
            </button>
          </div>

          <div className="settings-section">
            <p className="settings-section-title">Patienten QR-Codes</p>
            <button onClick={() => navigate('/CreatePatientQrCodePage')} className="wide-button">
              Patienten QR-Codes erzeugen
            </button>
            <button onClick={() => navigate('/ShowUnusedPatientQrCodesPage')} className="wide-button">
              Nicht verwendete QR-Codes anzeigen
            </button>
          </div>

          <div className="settings-section">
            <p className="settings-section-title">Einsatzort</p>
            <button onClick={() => navigate('/EditOperationScene')} className="wide-button" style={{ flex: '1 1 100%' }}>
              Einsatzort erstellen/bearbeiten
            </button>
          </div>

          <div className="settings-section">
            <p className="settings-section-title">Admin-Management</p>
            <button onClick={() => navigate('/CreateNewAdminUserPage')} className="wide-button" style={{ flex: '1 1 100%' }}>
              Admin-User erstellen
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
