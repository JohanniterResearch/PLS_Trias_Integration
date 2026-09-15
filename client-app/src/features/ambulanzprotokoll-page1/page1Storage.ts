import type { Page1PersistedRecord } from './page1State';

const storagePrefix = 'ambdoku:page1:patient';

export function page1StorageKey(patientId: number) {
  return `${storagePrefix}:${patientId}`;
}

export function readPage1Draft(patientId: number): Page1PersistedRecord | null {
  const raw = window.localStorage.getItem(page1StorageKey(patientId));
  if (!raw) return null;

  try {
    return JSON.parse(raw) as Page1PersistedRecord;
  } catch {
    return null;
  }
}

export function writePage1Draft(record: Page1PersistedRecord) {
  window.localStorage.setItem(page1StorageKey(record.patientId), JSON.stringify(record));
}

export function clearPage1Draft(patientId: number) {
  window.localStorage.removeItem(page1StorageKey(patientId));
}

export function comparePage1UpdatedAt(left: string | null, right: string | null) {
  const leftTime = left === null ? Number.NaN : Date.parse(left);
  const rightTime = right === null ? Number.NaN : Date.parse(right);

  if (Number.isNaN(leftTime) && Number.isNaN(rightTime)) return 0;
  if (Number.isNaN(leftTime)) return -1;
  if (Number.isNaN(rightTime)) return 1;
  return leftTime - rightTime;
}
