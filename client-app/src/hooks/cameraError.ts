export type CameraErrorKind = 'permission' | 'not-found' | 'insecure' | 'unavailable' | 'unsupported' | 'unknown';

export const cameraErrorMessages: Record<CameraErrorKind, string> = {
  permission: 'Der Kamerazugriff ist nicht erlaubt. Prüfen Sie die Kamera-Berechtigung in Ihrem Browser und in den Geräteeinstellungen.',
  'not-found': 'Keine verfügbare oder passende Kamera gefunden. Prüfen Sie, ob eine Kamera angeschlossen und aktiviert ist.',
  insecure: 'Der Kamerazugriff benötigt eine sichere Verbindung. Öffnen Sie diese Seite über HTTPS.',
  unavailable: 'Die Kamera konnte nicht gestartet werden. Schließen Sie gegebenenfalls andere Anwendungen, die die Kamera verwenden.',
  unsupported: 'Der Kamerazugriff wird in diesem Browser oder dieser Umgebung nicht unterstützt.',
  unknown: 'Die Kamera konnte nicht gestartet werden. Bitte versuchen Sie es erneut.',
};

// html5-qrcode 2.3.8 wraps getUserMedia errors in strings. Other errors
// may still be objects. Match error names, not arbitrary message prose.
export function classifyCameraError(error: unknown): CameraErrorKind {
  const name = typeof error === 'object' && error !== null && 'name' in error
    ? String(error.name) : '';
  const wrappedName = typeof error === 'string'
    ? /^(?:Error getting userMedia, error = )?([A-Za-z]+Error)(?=:|$)/.exec(error)?.[1]
    : undefined;
  switch (name || wrappedName) {
    case 'NotAllowedError':
    case 'PermissionDeniedError':
    case 'SecurityError':
      return 'permission';
    case 'NotFoundError':
    case 'DevicesNotFoundError':
    case 'OverconstrainedError':
    case 'ConstraintNotSatisfiedError':
      return 'not-found';
    case 'NotReadableError':
    case 'TrackStartError':
    case 'AbortError':
      return 'unavailable';
    default:
      return 'unknown';
  }
}
