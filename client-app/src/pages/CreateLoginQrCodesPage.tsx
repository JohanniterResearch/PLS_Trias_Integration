import { useState, useRef } from 'react';
import { QRCodeCanvas } from 'qrcode.react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { generateLoginQRCodes } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function CreateLoginQrCodesPage() {
  const navigate = useNavigate();
  const [numberOfQrCodes, setNumberOfQrCodes] = useState<number>(1);
  const [generatedCodes, setGeneratedCodes] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const { adminToken } = useAuthStore();
  const printRef = useRef<HTMLDivElement>(null);

  const handleGenerate = async (event: React.FormEvent) => {
    event.preventDefault();
    setIsLoading(true);
    setErrorMessage('');
    try {
      const { data } = await generateLoginQRCodes(numberOfQrCodes, adminToken!);
      if (data.status.toLowerCase() === 'success' && data.qrcodes) {
        setGeneratedCodes(data.qrcodes);
      } else {
        setErrorMessage('Fehler beim Generieren der QR-Codes.');
      }
    } catch (error) {
      console.error('Fetch error:', error);
      setErrorMessage('Fehler beim Generieren der QR-Codes. Bitte versuchen Sie es erneut.');
    } finally {
      setIsLoading(false);
    }
  };

  const handlePrint = () => {
    if (printRef.current) {
      const printWindow = window.open('', '_blank');
      if (printWindow) {
        printWindow.document.write('<html><head><title>Login QR-Codes</title>');
        printWindow.document.write(
          '<style>body { font-family: Arial, sans-serif; } .qr-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; padding: 20px; } .qr-item { text-align: center; page-break-inside: avoid; } @media print { .qr-grid { grid-template-columns: repeat(3, 1fr); } }</style>'
        );
        printWindow.document.write('</head><body>');
        printWindow.document.write(printRef.current.innerHTML);
        printWindow.document.write('</body></html>');
        printWindow.document.close();
        printWindow.print();
      }
    }
  };

  return (
    <>
      <Appbar />
      <div className="auth-page" style={{ alignItems: 'flex-start', paddingTop: '40px' }}>
        <div className="auth-card" style={{ maxWidth: '520px' }}>
          <h2>QR-Codes für die Authentifizierung generieren</h2>
          <p>Wählen Sie die Anzahl der zu generierenden QR-Codes aus.</p>

          {errorMessage && <p className="msg-error">{errorMessage}</p>}

          <form onSubmit={handleGenerate} style={{ marginTop: '16px' }}>
            <div className="form-group">
              <label htmlFor="numberOfQrCodes">Anzahl</label>
              <input
                type="number"
                id="numberOfQrCodes"
                min={1}
                max={100}
                value={numberOfQrCodes}
                onChange={(e) => setNumberOfQrCodes(parseInt(e.target.value, 10) || 1)}
              />
            </div>
            <p style={{ color: 'rgba(255,255,255,0.5)', fontSize: '0.85rem', marginBottom: '16px' }}>
              Geben Sie die gewünschte Anzahl an QR-Codes ein und klicken Sie auf &quot;Generieren&quot;.
            </p>
            <div style={{ display: 'flex', gap: '10px' }}>
              <button type="submit" disabled={isLoading} className="btn-primary">
                {isLoading ? 'Generiere...' : 'QR-Codes generieren'}
              </button>
              {generatedCodes.length > 0 && (
                <button type="button" onClick={handlePrint} className="btn-secondary" style={{ whiteSpace: 'nowrap' }}>
                  Als PDF drucken
                </button>
              )}
            </div>
          </form>

          <button onClick={() => navigate('/AdminSettingsPage')} className="btn-secondary" style={{ marginTop: '12px', width: '100%' }}>
            Zurück zu den Einstellungen
          </button>
        </div>
      </div>

      {generatedCodes.length > 0 && (
        <div className="page">
          <div ref={printRef}>
            <div
              className="qr-grid"
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(3, 1fr)',
                gap: '20px',
                padding: '20px',
                maxWidth: '700px',
                margin: '0 auto',
              }}
            >
              {generatedCodes.map((code, index) => (
                <div key={index} className="qr-item" style={{ textAlign: 'center', background: 'white', padding: '12px', borderRadius: '8px' }}>
                  <QRCodeCanvas value={code} size={150} />
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </>
  );
}
