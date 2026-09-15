import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import type { OperationScene } from '../types';
import { getAllCurrentOperationScenes, createOperationScene, deleteOperationScene } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function EditOperationScene() {
  const [operationScenes, setOperationScenes] = useState<OperationScene[]>([]);
  const [selectedScene, setSelectedScene] = useState<OperationScene | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const { adminToken } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => {
    if (!adminToken) {
      setIsLoading(false);
      setErrorMessage('Admin-Anmeldung erforderlich.');
      return;
    }

    const fetchScenes = async () => {
      try {
        const { data } = await getAllCurrentOperationScenes(adminToken!);
        setOperationScenes(data);
      } catch (error) {
        console.error('Fetch error:', error);
        setErrorMessage('Fehler beim Laden der Einsatzorte.');
      } finally {
        setIsLoading(false);
      }
    };
    fetchScenes();
  }, [adminToken]);

  const handleSceneSelect = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const sceneId = parseInt(event.target.value, 10);
    const scene = operationScenes.find((s) => s.idoperationScene === sceneId) || null;
    setSelectedScene(scene);
    if (scene) {
      setName(scene.name);
      setDescription(scene.description || '');
    } else {
      setName('');
      setDescription('');
    }
    setSuccessMessage('');
    setErrorMessage('');
  };

  const handleUpdate = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedScene) return;
    setSuccessMessage('');
    setErrorMessage('');

    try {
      const { data } = await createOperationScene(
        { id: selectedScene.idoperationScene, name, description },
        adminToken!
      );
      if (data.status?.toLowerCase() === 'success') {
        setSuccessMessage('Einsatzort wurde erfolgreich aktualisiert.');
        setOperationScenes((prev) =>
          prev.map((s) =>
            s.idoperationScene === selectedScene.idoperationScene
              ? { ...s, name, description }
              : s
          )
        );
      } else {
        setErrorMessage(data.message || 'Fehler beim Aktualisieren des Einsatzortes.');
      }
    } catch (error) {
      console.error('Fetch error:', error);
      setErrorMessage('Fehler beim Aktualisieren. Bitte versuchen Sie es erneut.');
    }
  };

  const handleDelete = async () => {
    if (!selectedScene) return;
    if (!window.confirm(`Einsatzort "${selectedScene.name}" wirklich löschen?`)) return;
    setSuccessMessage('');
    setErrorMessage('');

    try {
      const { data } = await deleteOperationScene(selectedScene.idoperationScene, adminToken!);
      if (data.status?.toLowerCase() === 'success') {
        setSuccessMessage('Einsatzort wurde erfolgreich gelöscht.');
        setOperationScenes((prev) =>
          prev.filter((s) => s.idoperationScene !== selectedScene.idoperationScene)
        );
        setSelectedScene(null);
        setName('');
        setDescription('');
      } else {
        setErrorMessage(data.message || 'Fehler beim Löschen des Einsatzortes.');
      }
    } catch (error) {
      console.error('Fetch error:', error);
      setErrorMessage('Fehler beim Löschen. Bitte versuchen Sie es erneut.');
    }
  };

  if (isLoading) return <><Appbar /><div className="auth-page"><p>Lade Einsatzorte...</p></div></>;

  return (
    <>
      <Appbar />
      <div className="auth-page" style={{ alignItems: 'flex-start', paddingTop: '40px' }}>
        <div className="auth-card" style={{ maxWidth: '500px' }}>
          <h2>Einsatzort bearbeiten</h2>
          <p style={{ marginBottom: '0' }}></p>

          {successMessage && <p className="msg-success">{successMessage}</p>}
          {errorMessage && <p className="msg-error">{errorMessage}</p>}

          <div style={{ marginTop: '16px' }}>
            {operationScenes.length === 0 ? (
              <p style={{ color: 'rgba(255,255,255,0.5)' }}>Keine Einsatzorte vorhanden.</p>
            ) : (
              <>
                <div className="form-group">
                  <label htmlFor="sceneSelect">Einsatzort auswählen:</label>
                  <select
                    id="sceneSelect"
                    value={selectedScene?.idoperationScene || ''}
                    onChange={handleSceneSelect}
                  >
                    <option value="">-- Einsatzort wählen --</option>
                    {operationScenes.map((scene) => (
                      <option key={scene.idoperationScene} value={scene.idoperationScene}>
                        {scene.name}
                      </option>
                    ))}
                  </select>
                </div>

                {selectedScene && (
                  <form onSubmit={handleUpdate}>
                    <div className="form-group">
                      <label htmlFor="name">Name des Einsatzorts:</label>
                      <input
                        type="text"
                        id="name"
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label htmlFor="description">Beschreibung:</label>
                      <textarea
                        id="description"
                        value={description}
                        onChange={(e) => setDescription(e.target.value)}
                      />
                    </div>
                    <button type="submit" className="btn-primary">
                      Einsatzort aktualisieren
                    </button>
                    <button
                      type="button"
                      onClick={handleDelete}
                      className="btn-danger"
                      style={{ marginTop: '10px', width: '100%' }}
                    >
                      Einsatzort löschen
                    </button>
                  </form>
                )}
              </>
            )}
          </div>

          <button
            onClick={() => navigate('/CreateOperationScene')}
            className="btn-secondary"
            style={{ marginTop: '16px', width: '100%' }}
          >
            Neuen Einsatzort erstellen
          </button>
          <button
            onClick={() => navigate('/AdminSettingsPage')}
            className="btn-secondary"
            style={{ marginTop: '8px', width: '100%' }}
          >
            Zurück zu den Einstellungen
          </button>
        </div>
      </div>
    </>
  );
}
