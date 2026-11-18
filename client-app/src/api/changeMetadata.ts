export interface RejectedChange {
  fieldId: number;
  field: string;
  reason: string;
  currentValue: unknown;
  currentChangedAt: string | null;
}

export function changeMetadata(changedAt = new Date().toISOString()) {
  return { changedAt };
}

export function changeFieldLabel(field: string): string {
  const labels: Record<string, string> = {
    'patient.atmung': 'Atmung', 'patient.blutung': 'Blutung',
    'patient.triagefarbe': 'Triagefarbe', 'patient.location': 'Position', 'form.status': 'Protokollstatus',
  };
  if (labels[field]) return labels[field];
  const name = field.split('.').at(-1)!.replaceAll('_', ' ');
  return `${field.startsWith('body.') ? 'Körperkarte: ' : ''}${name.charAt(0).toUpperCase()}${name.slice(1)}`;
}
