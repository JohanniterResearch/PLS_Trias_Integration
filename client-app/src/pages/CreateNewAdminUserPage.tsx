import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../stores/authStore';
import { createAdminUser } from '../api/endpoints';
import Appbar from '../components/Appbar';

export default function CreateNewAdminUserPage() {
  const [formData, setFormData] = useState({
    username: '',
    password: '',
    passwordConfirmation: '',
  });
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const { adminToken } = useAuthStore();
  const navigate = useNavigate();

  const handleInputChange = ({ target }: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSuccessMessage('');
    setErrorMessage('');

    if (formData.password !== formData.passwordConfirmation) {
      setErrorMessage('Die Passwörter stimmen nicht überein.');
      return;
    }

    if (formData.password.length < 8) {
      setErrorMessage('Das Passwort muss mindestens 8 Zeichen lang sein.');
      return;
    }

    setIsLoading(true);

    try {
      const { data } = await createAdminUser(formData.username, formData.password, adminToken!);
      if (data.status?.toLowerCase() === 'success') {
        setSuccessMessage('Admin-User wurde erfolgreich erstellt.');
        setFormData({ username: '', password: '', passwordConfirmation: '' });
      } else {
        setErrorMessage(data.message || 'Fehler beim Erstellen des Admin-Users.');
      }
    } catch (error) {
      console.error('Fetch error:', error);
      setErrorMessage('Fehler beim Erstellen des Admin-Users. Bitte versuchen Sie es erneut.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      <Appbar />
      <div className="auth-page" style={{ alignItems: 'flex-start', paddingTop: '40px' }}>
        <div className="auth-card" style={{ maxWidth: '460px' }}>
          <h2>Neuen Admin-Benutzer erstellen</h2>
          <p></p>

          {successMessage && <p className="msg-success">{successMessage}</p>}
          {errorMessage && <p className="msg-error">{errorMessage}</p>}

          <form onSubmit={handleSubmit} style={{ marginTop: '16px' }}>
            <div className="form-group">
              <label htmlFor="username">Benutzername:</label>
              <input
                type="text"
                id="username"
                name="username"
                value={formData.username}
                onChange={handleInputChange}
                placeholder="Benutzernamen eingeben"
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="password">Passwort:</label>
              <input
                type="password"
                id="password"
                name="password"
                value={formData.password}
                onChange={handleInputChange}
                placeholder="Passwort eingeben (mind. 8 Zeichen)"
                required
              />
            </div>
            <div className="form-group">
              <label htmlFor="passwordConfirmation">Passwort bestätigen:</label>
              <input
                type="password"
                id="passwordConfirmation"
                name="passwordConfirmation"
                value={formData.passwordConfirmation}
                onChange={handleInputChange}
                placeholder="Passwort bestätigen"
                required
              />
            </div>
            <button type="submit" disabled={isLoading} className="btn-primary">
              {isLoading ? 'Erstelle...' : 'Benutzer erstellen'}
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
