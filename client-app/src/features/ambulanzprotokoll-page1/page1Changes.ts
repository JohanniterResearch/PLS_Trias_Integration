import type { Page1State, PendingPage1Field } from './page1State';

// Arrays represent one field; omitted object properties are never sent as defaults.
export function flattenFields(value: unknown, prefix = ''): Record<string, unknown> {
  if (value !== null && typeof value === 'object' && !Array.isArray(value)) {
    return Object.assign({}, ...Object.entries(value).map(([key, child]) => flattenFields(child, prefix ? `${prefix}.${key}` : key)));
  }
  return { [prefix]: value };
}

function setPath(target: Record<string, unknown>, path: string, value: unknown) {
  const segments = path.split('.');
  let current = target;
  for (const segment of segments.slice(0, -1)) {
    current[segment] ??= {};
    current = current[segment] as Record<string, unknown>;
  }
  current[segments.at(-1)!] = value;
}

export function patchFields(state: Page1State, fields: Record<string, PendingPage1Field>): Page1State {
  const next = structuredClone(state);
  for (const [path, entry] of Object.entries(fields)) setPath(next as unknown as Record<string, unknown>, path, entry.value);
  return next;
}

export function pendingPayload(fields: Record<string, PendingPage1Field>, finalizeAt: string | null) {
  const formState: Record<string, unknown> = {};
  const fieldChangedAt: Record<string, string> = {};
  for (const [path, entry] of Object.entries(fields)) {
    setPath(formState, path, entry.value);
    fieldChangedAt[`form.${path}`] = entry.changedAt;
  }
  if (finalizeAt) fieldChangedAt['form.status'] = finalizeAt;
  return {
    formState, fieldChangedAt,
    changedAt: finalizeAt ?? Object.values(fields).map(p => p.changedAt).sort().at(-1)!,
    status: finalizeAt ? 'finalized' as const : 'draft' as const,
  };
}
