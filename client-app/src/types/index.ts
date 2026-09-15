export interface Patient {
  idpatient: number;
  atmung: string | null;
  blutung: string | null;
  radialispuls: string | null;
  triagefarbe: string | null;
  transport: string | null;
  dringend: string | null;
  kontaminiert: string | null;
  name: string | null;
  longitude_patient: number | null;
  latitude_patient: number | null;
  user_iduser: number | null;
  created_at: string | null;
  updated_at: string | null;
}

export interface OperationScene {
  idoperationScene: number;
  name: string;
  description: string | null;
  created_at: string | null;
  updated_at: string | null;
}

export interface BodyParts {
  [key: string]: number;
}

export interface AuthResponse {
  status: string;
  token?: string;
  message?: string;
  refreshToken?: string;
  requiresPasswordChange?: boolean;
}

export interface GenerateQrCodesResponse {
  status: string;
  qrcodes: string[];
}
