import { useState, useEffect, useRef } from 'react';
import { QRCodeCanvas } from 'qrcode.react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { getUnusedPatientQrCodes } from '../api/endpoints';

export default function ShowUnusedPatientQrCodesPage() {
  const navigate = useNavigate();
  const [unusedCodes, setUnusedCodes] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const { adminToken } = useAuthStore();
  const printRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const fetchUnusedCodes = async () => {
      try {
        const { data } = await getUnusedPatientQrCodes(adminToken!);
        setUnusedCodes(Array.isArray(data) ? data : []);
      } catch (error) {
        console.error('Fetch error:', error);
        setErrorMessage('Fehler beim Laden der nicht verwendeten QR-Codes.');
      } finally {
        setIsLoading(false);
      }
    };
    fetchUnusedCodes();
  }, [adminToken]);

  const handlePrint = () => {
    if (printRef.current) {
      const printWindow = window.open('', '_blank');
      if (printWindow) {
        printWindow.document.write('<html><head><title>Nicht verwendete Patienten QR-Codes</title>');
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

  if (isLoading) return <p>Lade nicht verwendete Patienten QR-Codes...</p>;

  return (
    <div style={{ padding: '20px', maxWidth: '800px', margin: '0 auto' }}>
      <h2>Nicht verwendete Patienten QR-Codes</h2>
      <button onClick={() => navigate('/AdminSettingsPage')} style={{ marginBottom: '15px' }}>
        Zurück zu den Einstellungen
      </button>

      {errorMessage && (
        <p style={{ color: 'red', fontWeight: 'bold' }}>{errorMessage}</p>
      )}

      {unusedCodes.length === 0 && !errorMessage ? (
        <p>Keine nicht verwendeten Patienten QR-Codes vorhanden.</p>
      ) : (
        <div>
          <p>Anzahl: {unusedCodes.length}</p>
          <button onClick={handlePrint} style={{ marginBottom: '15px' }}>
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
              {unusedCodes.map((code, index) => (
                <div key={index} className="qr-item" style={{ textAlign: 'center', background: 'white', padding: '12px', borderRadius: '8px' }}>
                  <QRCodeCanvas value={code} size={150} />
                  <p style={{ fontSize: '12px', marginTop: '5px', wordBreak: 'break-all' }}>
                    {code}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
