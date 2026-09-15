import { useState, useRef } from 'react';
import { QRCodeCanvas } from 'qrcode.react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { generatePatientQRCodes } from '../api/endpoints';

export default function CreatePatientQrCodes() {
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
      const { data } = await generatePatientQRCodes(numberOfQrCodes, adminToken!);
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
        printWindow.document.write('<html><head><title>Patienten QR-Codes</title>');
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
    <div style={{ padding: '20px', maxWidth: '800px', margin: '0 auto' }}>
      <h2>Patienten QR-Codes erzeugen</h2>
      <form onSubmit={handleGenerate}>
        <div style={{ marginBottom: '15px' }}>
          <label htmlFor="numberOfQrCodes">Anzahl der QR-Codes:</label>
          <input
            type="number"
            id="numberOfQrCodes"
            min={1}
            max={100}
            value={numberOfQrCodes}
            onChange={(e) => setNumberOfQrCodes(parseInt(e.target.value, 10) || 1)}
            style={{ marginLeft: '10px', width: '80px' }}
          />
        </div>
        <button type="submit" disabled={isLoading}>
          {isLoading ? 'Generiere...' : 'QR-Codes generieren'}
        </button>
      </form>

      <button onClick={() => navigate('/AdminSettingsPage')} style={{ marginTop: '15px' }}>
        Zurück zu den Einstellungen
      </button>

      {errorMessage && (
        <p style={{ color: 'red', fontWeight: 'bold' }}>{errorMessage}</p>
      )}

      {generatedCodes.length > 0 && (
        <div>
          <button onClick={handlePrint} style={{ marginTop: '15px', marginBottom: '15px' }}>
            Drucken
          </button>
          <div ref={printRef}>
            <div
              className="qr-grid"
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(3, 1fr)',
                gap: '20px',
                padding: '20px',
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
    </div>
  );
}
