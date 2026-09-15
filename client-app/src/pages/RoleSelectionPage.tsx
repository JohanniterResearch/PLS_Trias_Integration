import { useEffect, useState } from 'react';
import { useAuthStore } from '../stores/authStore';
import { useNavigate } from 'react-router-dom';
import type { OperationScene } from '../types';
import { getAllCurrentOperationScenes, selectScene } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function RoleSelectionPage() {
  const [selectedRole, setSelectedRole] = useState('');
  const { token, setToken } = useAuthStore();
  const [selectedOperationScene, setSelectedOperationScene] = useState('');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [existingOperationScenes, setExistingOperationScenes] = useState<OperationScene[]>([]);

  const handleOptionChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedOperationScene(event.target.value);
  };

  const handleRoleChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    setSelectedRole(event.target.value);
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    const operationSceneId = Number(selectedOperationScene);
    const role = selectedRole.toUpperCase();
    if (role !== 'TRIAGE' && role !== 'LEITSTELLE') {
      navigate('/NotFound');
      return;
    }

    if (!token) {
      setErrorMessage('Keine gueltige Anmeldung gefunden. Bitte erneut anmelden.');
      return;
    }

    setSubmitting(true);
    setErrorMessage('');
    try {
      // Patient endpoints require the scene claim minted here — the plain
      // login token has none, so this must happen before navigating.
      const { data } = await selectScene(operationSceneId, token);
      setToken(data.token);
      navigate(role === 'TRIAGE' ? '/ScanPatient' : '/SituationRoomTable', {
        state: { operationSceneId },
      });
    } catch (error) {
      console.error('Einsatzort konnte nicht ausgewählt werden:', error);
      setErrorMessage('Einsatzort konnte nicht ausgewählt werden. Bitte erneut versuchen.');
    } finally {
      setSubmitting(false);
    }
  };

  useEffect(() => {
    const fetchExistingScenes = async () => {
      if (!token) {
        setErrorMessage('Keine gueltige Anmeldung gefunden. Bitte erneut anmelden.');
        setLoading(false);
        return;
      }

      try {
        const { data } = await getAllCurrentOperationScenes(token);
        setExistingOperationScenes(data);
      } catch (error) {
        console.error('Einsatzort konnte nicht geladen werden:', error);
        setErrorMessage('Einsatzorte konnten nicht geladen werden. Bitte Seite neu laden.');
      } finally {
        setLoading(false);
      }
    };
    fetchExistingScenes();
  }, [token]);

  return (
    <>
      <Appbar />
      <div className="auth-page" style={{ alignItems: 'flex-start', paddingTop: '40px' }}>
        <div className="auth-card" style={{ maxWidth: '460px' }}>
          {loading ? (
            <p style={{ textAlign: 'center', color: 'rgba(255,255,255,0.5)' }}>Lade bestehende Einsatzorte...</p>
          ) : errorMessage ? (
            <div>
              <p className="msg-error">{errorMessage}</p>
              <button onClick={() => window.location.reload()} className="btn-primary" style={{ marginBottom: '10px' }}>
                Erneut versuchen
              </button>
              <button onClick={() => navigate('/AdminLandingPage')} className="btn-secondary" style={{ width: '100%' }}>
                Zur Admin-Anmeldung
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <h2 style={{ marginBottom: '6px' }}>Rollenauswahl</h2>
              <p style={{ color: 'rgba(255,255,255,0.5)', fontSize: '0.9rem', marginBottom: '20px' }}>
                Wählen Sie Einsatzort und Funktion aus.
              </p>

              <div className="form-group">
                <label>Einsatzort:</label>
                <select value={selectedOperationScene} onChange={handleOptionChange} required>
                  <option value="">Wähle einen Einsatzort aus</option>
                  {existingOperationScenes.map((scene) => (
                    <option key={scene.idoperationScene} value={scene.idoperationScene}>
                      {scene.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="form-group">
                <label>Funktion:</label>
                <select value={selectedRole} onChange={handleRoleChange} required>
                  <option value="">Rolle auswählen</option>
                  <option value="Triage">Triage</option>
                  <option value="Leitstelle">Leitstelle</option>
                </select>
              </div>

              <button type="submit" className="btn-primary" style={{ marginBottom: '10px' }} disabled={submitting}>
                {submitting ? 'Bestätige...' : 'Bestätigen'}
              </button>
              <button type="button" onClick={() => navigate('/AdminLandingPage')} className="btn-secondary" style={{ width: '100%' }}>
                Zur Admin-Anmeldung
              </button>
            </form>
          )}
        </div>
      </div>
    </>
  );
}
