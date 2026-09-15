import client from './client';
import type { AuthResponse, Patient, OperationScene, BodyParts, GenerateQrCodesResponse } from '../types';
import type { Page1PersistedRecord, Page1State } from '../features/ambulanzprotokoll-page1/page1State';
import type { TriageColor } from '../types/triageColor';

// Auth
export const qrLogin = (qr_code: string) =>
  client.post<AuthResponse>('/qr-login', { qr_code });

export const userLogin = (username: string, password: string) =>
  client.post<AuthResponse>('/user-login', { username, password });

export const adminLogin = (username: string, password: string) =>
  client.post<AuthResponse>('/adminLogin', { username, password });

// DEV WORKAROUND — remove before production
export const devLogin = (role: 'admin' | 'user') =>
  client.post<AuthResponse>('/dev-login', { role });

// Persons — v2 endpoints (typed operationSceneId, boolean respiration/blutung)
export const getPersons = (operationSceneId: number, token: string) =>
  client.post<Patient[]>('/v2/persons', { operationSceneId }, { headers: { Authorization: `Bearer ${token}` } });

export const updateTriageColor = (patientId: number, data: {
  triageColor?: TriageColor; respiration?: boolean; blutung?: boolean;
}, token: string) =>
  client.post(`/v2/persons/${patientId}/update-triage-color`, data, { headers: { Authorization: `Bearer ${token}` } });

export const updateRespiration = (patientId: number, data: {
  respiration: boolean;
}, token: string) =>
  client.post(`/v2/persons/${patientId}/respiration`, data, { headers: { Authorization: `Bearer ${token}` } });

export const updatePatientLocation = (patientId: number, data: {
  lat: number; lng: number;
}, token: string) =>
  client.post(`/v2/persons/${patientId}/location`, data, { headers: { Authorization: `Bearer ${token}` } });

export const verifyPatientQrCode = (qr_code: string, operationSceneId: number, token: string) =>
  client.post<{ patientId: number }>('/v2/verify-patient-qr-code', { qr_code, operationSceneId }, { headers: { Authorization: `Bearer ${token}` } });

// Body Parts
export const saveBodyPart = (bodyPartId: string, isClicked: number, idpatient: number, token: string) =>
  client.put('/save-body-part', { bodyPartId, isClicked, idpatient }, { headers: { Authorization: `Bearer ${token}` } });

export const getBodyParts = (idpatient: number, token: string) =>
  client.get<BodyParts>(`/get-body-parts?idpatient=${idpatient}`, { headers: { Authorization: `Bearer ${token}` } });

// Ambulanzprotokoll page 1
export const getAmbulanzprotokollPage1 = (patientId: number, token?: string) =>
  client.get<Page1PersistedRecord | null>(`/v2/persons/${patientId}/ambulanzprotokoll-page1`, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });

export const putAmbulanzprotokollPage1 = (
  patientId: number,
  payload: {
    formState: Page1State;
    status: 'draft' | 'finalized';
    finalizedAt: string | null;
    updatedAt: string;
  },
  token?: string,
) =>
  client.put<Page1PersistedRecord>(`/v2/persons/${patientId}/ambulanzprotokoll-page1`, payload, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  });

// QR Codes
export const generatePatientQRCodes = (number: number, token: string) =>
  client.post<GenerateQrCodesResponse>('/generatePatientQRCodes', { number }, { headers: { Authorization: `Bearer ${token}` } });

export const getUnusedPatientQrCodes = (token: string) =>
  client.get<string[]>('/getUnusedPatientQrCodes', { headers: { Authorization: `Bearer ${token}` } });

// DEV WORKAROUND — remove before production
export const getUnusedPatientQrCodesDev = (token: string) =>
  client.get<string[]>('/dev/unusedPatientQrCodes', { headers: { Authorization: `Bearer ${token}` } });

export const generateLoginQRCodes = (number: number, token: string) =>
  client.post<GenerateQrCodesResponse>('/generateLoginQRCodes', { number }, { headers: { Authorization: `Bearer ${token}` } });

export const getLoginQrCodes = (token: string) =>
  client.get<string[]>('/getLoginQrCodes', { headers: { Authorization: `Bearer ${token}` } });

// Operation Scenes — v2 endpoints (correct "operationScene" response key)
export const createOperationScene = (data: { id?: number; name: string; description?: string }, token: string) =>
  client.post('/v2/createOperationScene', data, { headers: { Authorization: `Bearer ${token}` } });

export const selectScene = (operationSceneId: number, token: string) =>
  client.post<{ status: string; token: string }>(
    '/v2/select-scene',
    { operationSceneId },
    { headers: { Authorization: `Bearer ${token}` } }
  );

export const getAllCurrentOperationScenes = (token: string) =>
  client.get<OperationScene[]>('/v2/getAllCurrentOperationScenes', { headers: { Authorization: `Bearer ${token}` } });

export const deleteOperationScene = (id: number, token: string) =>
  client.delete(`/v2/deleteOperationScene/${id}`, { headers: { Authorization: `Bearer ${token}` } });

// Users
export const createAdminUser = (username: string, password: string, token: string) =>
  client.post('/createAdminUser', { username, password }, { headers: { Authorization: `Bearer ${token}` } });

export const changeAdminPassword = (username: string, password: string, newpassword: string, token: string) =>
  client.post('/changeAdminPassword', { username, password, newpassword }, { headers: { Authorization: `Bearer ${token}` } });
