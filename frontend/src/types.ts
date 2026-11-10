export const HOLD_STATUS = {
  HELD: 'HELD',
  CONFIRMED: 'CONFIRMED',
  EXPIRED: 'EXPIRED',
  CANCELLED: 'CANCELLED',
} as const;

export type HoldStatus = (typeof HOLD_STATUS)[keyof typeof HOLD_STATUS];

export interface CreateClassRequest {
  name: string;
  totalSeats: number;
}

/** 1. POST /classes (201) */
export interface ClassCreatedResponse {
  id: string;
  name: string;
  totalSeats: number;
}

/** 2. GET /classes (200) & 3. GET /classes/{classId} (200) */
export interface ClassSeatsResponse {
  id: string;
  name: string;
  totalSeats: number;
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

/** 4. POST /classes/{classId}/holds (201) */
export interface HoldCreatedResponse {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  serverTime?: string;
}

/** 5. GET /holds/{holdId} (200) */
export interface HoldResponse {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  serverTime?: string;
}

/** 6. POST /holds/{holdId}/confirm (200) */
export interface HoldConfirmedResponse {
  holdId: string;
  status: HoldStatus;
}

/** 8. GET /me/holds (200) */
export interface MyHoldResponse {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  serverTime?: string;
}

export interface ProblemDetailsPayload {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  message?: string;
}

