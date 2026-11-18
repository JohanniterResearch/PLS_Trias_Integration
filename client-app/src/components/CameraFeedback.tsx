import { cameraErrorMessages, type CameraErrorKind } from '../hooks/cameraError';

interface CameraFeedbackProps {
  error: CameraErrorKind | null;
  isStarting: boolean;
  onRetry: () => void;
}

export default function CameraFeedback({ error, isStarting, onRetry }: CameraFeedbackProps) {
  if (isStarting) return <p role="status">Kamera wird gestartet. Bitte bestätigen Sie gegebenenfalls die Kamera-Freigabe im Browser.</p>;
  if (!error) return null;
  const canRetry = error !== 'insecure' && error !== 'unsupported';
  return (
    <div className="camera-error">
      <p role="alert">{cameraErrorMessages[error]}</p>
      {canRetry && (
        <button type="button" className="btn-secondary" onClick={onRetry}>
          Erneut versuchen
        </button>
      )}
    </div>
  );
}
