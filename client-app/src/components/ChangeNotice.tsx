import { useEffect, useState } from 'react';
import { changeFieldLabel } from '../api/changeMetadata';
import type { RejectedChange } from '../api/changeMetadata';

export default function ChangeNotice() {
  const [changes, setChanges] = useState<RejectedChange[]>([]);
  useEffect(() => {
    const receive = (event: Event) => setChanges((event as CustomEvent<RejectedChange[]>).detail);
    window.addEventListener('pls:rejected-changes', receive);
    return () => window.removeEventListener('pls:rejected-changes', receive);
  }, []);
  if (!changes.length) return null;
  return <aside role="alert" style={{ position: 'fixed', bottom: 16, left: 16, right: 16, zIndex: 2000, background: '#fff3cd', color: '#332701', padding: 16, border: '1px solid #856404' }}>
    <strong>Änderungen verworfen</strong>
    <p>Der gespeicherte Stand bleibt gültig. Bitte die betroffenen Angaben prüfen.</p>
    <ul>{changes.map((change) => <li key={change.fieldId}>{changeFieldLabel(change.field)}: {change.reason === 'older_timestamp' ? 'Neuere Änderung vorhanden' : change.reason === 'equal_timestamp' ? 'Unterschiedliche Werte bei gleicher Änderungszeit' : 'Bearbeitungsstand gesperrt oder bereits verarbeitet'}. Gültiger Wert: {formatValue(change.currentValue)}</li>)}</ul>
    <button type="button" onClick={() => setChanges([])}>Schließen</button>
  </aside>;
}

function formatValue(value: unknown): string {
  if (value === null) return 'Leer';
  if (typeof value === 'boolean') return value ? 'Ja' : 'Nein';
  const text = typeof value === 'string' ? value : JSON.stringify(value);
  return text.length > 100 ? `${text.slice(0, 100)}…` : text;
}
