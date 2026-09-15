import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { createOperationScene } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function CreateOperationScene() {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { adminToken } = useAuthStore();
  const navigate = useNavigate();

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setIsLoading(true);
    setSuccessMessage('');
    setErrorMessage('');

    try {
      const { data } = await createOperationScene({ name, description }, adminToken!);
      if (data.status?.toLowerCase() === 'success') {
        setSuccessMessage('Einsatzort wurde erfolgreich erstellt.');
        setName('');
        setDescription('');
      } else {
        setErrorMessage(data.message || 'Fehler beim Erstellen des Einsatzortes.');
      }
    } catch (error) {
      console.error('Fetch error:', error);
      setErrorMessage('Fehler beim Erstellen des Einsatzortes. Bitte versuchen Sie es erneut.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      <Appbar />
      <div className="auth-page" style={{ alignItems: 'flex-start', paddingTop: '40px' }}>
        <div className="auth-card" style={{ maxWidth: '500px' }}>
          <h2>Neuen Einsatzort erstellen</h2>
          <p>Der Einsatzort steht nach Erstellung 20 Tage in der Liste der Einsatzorte zur Auswahl.</p>

          {successMessage && <p className="msg-success">{successMessage}</p>}
          {errorMessage && <p className="msg-error">{errorMessage}</p>}

          <form onSubmit={handleSubmit} style={{ marginTop: '16px' }}>
            <div className="form-group">
              <label htmlFor="name">Name des Einsatzorts:</label>
              <input
                type="text"
                id="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Name des Einsatzorts eingeben"
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="description">Beschreibung:</label>
              <textarea
                id="description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Beschreibung des Einsatzorts eingeben"
              />
            </div>
            <button type="submit" disabled={isLoading} className="btn-primary">
              {isLoading ? 'Erstelle...' : 'Einsatzort erstellen'}
            </button>
          </form>

          <button onClick={() => navigate('/AdminSettingsPage')} className="btn-secondary" style={{ marginTop: '12px', width: '100%' }}>
            Zurück zu den Einstellungen
          </button>
        </div>
      </div>
    </>
  );
}
