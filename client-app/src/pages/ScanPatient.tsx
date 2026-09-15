import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { useQrScanner } from '../hooks/useQrScanner';
import { verifyPatientQrCode, getUnusedPatientQrCodesDev } from '../api/endpoints';
import { usePatientLocationStore } from '../stores/patientLocationStore';

function getPatientQrErrorMessage(error: unknown): string {
  const response = (error as { response?: { status?: number; data?: { message?: string } } })?.response;

  if (response?.status === 401) return 'Anmeldung abgelaufen. Bitte erneut anmelden.';
  if (response?.status === 404) return response.data?.message ?? 'Ungültiger Patienten QR-Code.';
  if (response?.status) return `Patienten QR-Code konnte nicht verarbeitet werden. Status: ${response.status}`;

  return 'Patienten QR-Code konnte nicht verarbeitet werden.';
}

export default function ScanPatient() {
  const location = useLocation();
  const { token } = useAuthStore();
  const ensurePatientLocation = usePatientLocationStore((state) => state.ensurePatientLocation);
  const operationSceneId: number = location.state?.operationSceneId;
  const navigate = useNavigate();
  const devButtonsEnabled = import.meta.env.VITE_ENABLE_DEV_BUTTONS === 'true';

  // DEV WORKAROUND — remove before production
  const [unusedQrCodes, setUnusedQrCodes] = useState<string[]>([]);
  const [selectedQrCode, setSelectedQrCode] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (!devButtonsEnabled || !token) return;
    getUnusedPatientQrCodesDev(token!)
      .then(({ data }) => setUnusedQrCodes(data))
      .catch((error) => console.error('Fehler beim Laden der QR-Codes', error));
  }, [devButtonsEnabled, token]);
  // END DEV WORKAROUND

  const onScanSuccess = (decodedText: string) => {
    if (!operationSceneId || operationSceneId < 1) {
      setErrorMessage('Bitte zuerst einen Einsatzort in der Rollenauswahl auswählen.');
      return;
    }

    setErrorMessage('');
    verifyPatientQrCode(decodedText, operationSceneId, token!)
      .then(({ data }) => {
        if (data.patientId) {
          void ensurePatientLocation(data.patientId);
          stopScanner();
          navigate('/TriagePage1', {
            state: { patientId: data.patientId, operationSceneId },
          });
        } else {
          setErrorMessage('Ungültiger QR-Code oder Fehler bei der Erstellung des Patienten.');
        }
      })
      .catch((error) => {
        console.error('Fehler beim Senden des QR-Codes', error);
        setErrorMessage(getPatientQrErrorMessage(error));
      });
  };

  const { cameraBlocked, stopScanner } = useQrScanner({
    elementId: 'reader',
    fps: 5,
    qrbox: { width: 200, height: 200 },
    onSuccess: onScanSuccess,
  });

  return (
    <div>
      <h2>Bitte einen Patienten QR-Code scannen</h2>
      <div id="reader"></div>
      {cameraBlocked && (
        <p style={{ color: 'red', fontWeight: 'bold' }}>
          Der Zugriff auf die Kamera wurde verweigert. Bitte erlauben Sie den
          Zugriff auf die Kamera über die Einstellungen.
        </p>
      )}
      {errorMessage && (
        <p style={{ color: 'red', fontWeight: 'bold' }}>{errorMessage}</p>
      )}
      <div style={{ marginTop: '1rem' }}>
        <button onClick={() => navigate('/RoleSelection')}>
          Zur Rollenauswahl
        </button>
      </div>
      {devButtonsEnabled && (
        <>
          {/* DEV WORKAROUND — remove before production */}
          <div style={{ marginTop: '2rem', border: '2px dashed orange', padding: '1rem' }}>
            <p style={{ color: 'orange', fontWeight: 'bold' }}>⚠ DEV ONLY — vor Produktion entfernen</p>
            <select
              value={selectedQrCode}
              onChange={(e) => setSelectedQrCode(e.target.value)}
              style={{ textAlign: 'center' }}
            >
              <option value="">QR-Code aus Liste wählen</option>
              {unusedQrCodes.map((code) => (
                <option key={code} value={code}>
                  {code.slice(0, 12)}…
                </option>
              ))}
            </select>
            <button
              disabled={!selectedQrCode}
              onClick={() => onScanSuccess(selectedQrCode)}
            >
              Auswählen
            </button>
          </div>
          {/* END DEV WORKAROUND */}
        </>
      )}
    </div>
  );
}
