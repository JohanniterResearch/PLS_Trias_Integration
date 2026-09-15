import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { useQrScanner } from '../hooks/useQrScanner';
import { qrLogin, userLogin, devLogin } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function QRAuthenticator() {
  const [accessGranted, setAccessGranted] = useState(false);
  const [showLogin, setShowLogin] = useState(false);
  const [loginData, setLoginData] = useState({ username: '', password: '' });
  const [loginError, setLoginError] = useState('');
  const { setToken, setAdminToken } = useAuthStore();
  const navigate = useNavigate();
  const devButtonsEnabled = import.meta.env.VITE_ENABLE_DEV_BUTTONS === 'true';

  const onScanSuccess = (decodedText: string) => {
    qrLogin(decodedText)
      .then(({ data }) => {
        if (data.status.toLowerCase() === 'success' && data.token) {
          setAdminToken(null);
          setToken(data.token);
          stopScanner();
          setAccessGranted(true);
        } else {
          console.error('Ungültiger QR-Code');
        }
      })
      .catch((error) => console.error('Fetch error:', error));
  };

  const { cameraBlocked, stopScanner } = useQrScanner({
    elementId: 'reader',
    fps: 10,
    qrbox: { width: 450, height: 450 },
    onSuccess: onScanSuccess,
  });

  const handleAdminLogin = () => {
    navigate('/AdminLandingPage');
  };

  const handleUserLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError('');
    try {
      const { data } = await userLogin(loginData.username, loginData.password);
      if (data.status.toLowerCase() === 'success' && data.token) {
        setAdminToken(null);
        setToken(data.token);
        setAccessGranted(true);
      } else {
        setLoginError('Benutzername oder Passwort ist falsch.');
      }
    } catch (error) {
      console.error('Login error:', error);
      setLoginError('Fehler bei der Anmeldung. Bitte versuche es nochmal.');
    }
  };

  return (
    <>
      <Appbar />
      <div className="qr-page">
        <h1>Willkommen im PLS-System</h1>
        <p style={{ color: 'rgba(255,255,255,0.6)', marginTop: '6px', fontSize: '0.95rem' }}>
          Scannen Sie Ihren QR-Code, um sich als Einsatzkraft anzumelden.
        </p>

        <div className="qr-hints">
          <h2>Hinweis</h2>
          <ul>
            <li><strong>Admin:</strong> Einsatzort erstellen, QR-Codes für Einsatzkräfte generieren.
              <br />
              <button
                onClick={handleAdminLogin}
                className="btn-secondary"
                style={{ marginTop: '8px', fontSize: '0.85rem', padding: '6px 14px' }}
                title="Admins erstellen zuerst einen Einsatzort und bei Bedarf QR-Codes für Einsatzkräfte und Patienten."
              >
                Adminanmeldung
              </button>
            </li>
            <li><strong>Einsatzkräfte:</strong> QR-Code von der Kamera erfassen lassen.</li>
          </ul>
        </div>

        <div className="qr-scanner-wrap">
          <div
            id="reader"
            title="Scannen Sie Ihren QR-Code, um sich als Einsatzkraft zu registrieren."
          />
        </div>

        {cameraBlocked && (
          <div className="camera-error">
            Zugriff auf die Kamera wurde verweigert. Bitte erlauben Sie den
            Kamerazugriff in den Browser-Einstellungen.
          </div>
        )}

        <div style={{ marginTop: '20px', width: '100%', maxWidth: '500px' }}>
          <button onClick={() => setShowLogin((v) => !v)} className="btn-secondary">
            {showLogin ? 'Anmeldung ausblenden' : 'Mit Benutzername anmelden'}
          </button>
          {showLogin && (
            <form onSubmit={handleUserLogin} style={{ marginTop: '12px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
              <input
                type="text"
                placeholder="Benutzername"
                value={loginData.username}
                onChange={(e) => setLoginData((d) => ({ ...d, username: e.target.value }))}
                required
              />
              <input
                type="password"
                placeholder="Passwort"
                value={loginData.password}
                onChange={(e) => setLoginData((d) => ({ ...d, password: e.target.value }))}
                required
              />
              <button type="submit" className="btn-primary">Anmelden</button>
              {loginError && <p className="msg-error">{loginError}</p>}
            </form>
          )}
        </div>

        {devButtonsEnabled && (
          <div style={{ marginTop: '20px' }}>
            <button
              onClick={async () => {
                try {
                  const { data } = await devLogin('user');
                  if (data.token) {
                    setAdminToken(null);
                    setToken(data.token);
                    setAccessGranted(true);
                  } else {
                    setLoginError('DEV-Login fehlgeschlagen: ungültige Antwort.');
                  }
                } catch (error) {
                  console.error('Dev login error:', error);
                  setLoginError('DEV-Login fehlgeschlagen. Ist Features:EnableDevLogin aktiv?');
                }
              }}
              style={{ background: '#e53e3e', color: 'white', padding: '10px 20px', border: '2px dashed #feb2b2', borderRadius: '6px', fontWeight: 'bold', cursor: 'pointer' }}
            >
              DEV: Skip to Role Selection
            </button>
            {loginError && <p className="msg-error">{loginError}</p>}
          </div>
        )}

        {accessGranted && <Navigate to="/RoleSelection" />}
      </div>
    </>
  );
}
