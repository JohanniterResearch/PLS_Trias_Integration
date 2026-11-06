import client from '../../api/client';
import { changeMetadata } from '../../api/changeMetadata';
import type { Page1PersistedRecord } from './page1State';

export interface Page1SavePayload {
  formState: Record<string, unknown>;
  changedAt: string;
  fieldChangedAt: Record<string, string>;
  status: 'draft' | 'finalized';
}

export type Page1ServerRecord = Omit<Page1PersistedRecord, 'operationSceneId'>;

export const getPage1 = (patientId: number, token?: string) =>
  client.get<Page1ServerRecord | null>(`/v2/persons/${patientId}/ambulanzprotokoll-page1`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });

export const putPage1 = (patientId: number, payload: Page1SavePayload, token?: string) =>
  client.put<Page1ServerRecord>(`/v2/persons/${patientId}/ambulanzprotokoll-page1`, { ...changeMetadata(payload.changedAt), ...payload }, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });
