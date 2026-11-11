import { API_URL } from './config';
import type {
  ClassCreatedResponse,
  ClassSeatsResponse,
  CreateClassRequest,
  HoldConfirmedResponse,
  HoldCreatedResponse,
  HoldResponse,
  MyHoldResponse,
  ProblemDetailsPayload,
} from './types';

export const STUDENT_ID_REGEX = /^\d{17}$/;

export const normalizeStudentId = (v: string): string =>
  v.trim().replace(/[০-৯]/g, (d) => String('০১২৩৪৫৬৭৮৯'.indexOf(d)));

export function isValidStudentId(value: string): boolean {
  return STUDENT_ID_REGEX.test(normalizeStudentId(value));
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly detail?: string;
  readonly isNetworkError: boolean;

  constructor(
    status: number,
    code: string,
    message: string,
    detail?: string,
    isNetworkError = false,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.detail = detail;
    this.isNetworkError = isNetworkError;
  }
}

function mapErrorMessage(
  status: number,
  code?: string,
  detail?: string,
): string {
  switch (code) {
    case 'NO_SEATS':
      return 'এই class-এ কোনো খালি seat নেই (No available seats in this class).';
    case 'DUPLICATE_HOLD':
      return 'এই class-এ আপনার ইতিমধ্যে একটি চলমান আবেদন বা নিশ্চিত ভর্তি রয়েছে (You already have an active hold or confirmed seat in this class).';
    case 'HOLD_EXPIRED':
      return 'আবেদনের সময়সীমা শেষ হয়ে গেছে — অনুগ্রহ করে নতুন করে আবেদন করুন (Your seat hold has expired).';
    case 'HOLD_NOT_ACTIVE':
      return 'শুধুমাত্র চলমান (HELD) আবেদন নিশ্চিত বা বাতিল করা যাবে (Only an active HELD application can be confirmed or cancelled).';
    case 'MISSING_STUDENT_ID':
      return '১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর দিন (Please provide a valid 17-digit Birth Registration Number).';
    case 'VALIDATION_ERROR':
      return detail
        ? `অবৈধ তথ্য দেওয়া হয়েছে: ${detail}`
        : 'অবৈধ তথ্য দেওয়া হয়েছে — অনুগ্রহ করে সব ঘর সঠিকভাবে পূরণ করুন (Invalid input).';
    case 'FORBIDDEN':
      return 'এই আবেদনটি অন্য শিক্ষার্থীর — আপনার এটি পরিবর্তন করার অনুমতি নেই (Forbidden: This application belongs to another student).';
    case 'NOT_FOUND':
      return 'চাহিদাকৃত class বা আবেদনটি পাওয়া যায়নি (Requested class or application was not found).';
    case 'CONFLICT':
      return detail
        ? `অনুরোধটি সম্পন্ন করা যায়নি: ${detail}`
        : 'অনুরোধটি বর্তমান অবস্থার সাথে সাংঘর্ষিক (Conflict with current state).';
    default:
      break;
  }

  // Fallback by HTTP status when response has no machine-readable `code`
  if (status === 400) {
    return detail
      ? `অবৈধ তথ্য দেওয়া হয়েছে (400): ${detail}`
      : 'অবৈধ তথ্য দেওয়া হয়েছে — অনুগ্রহ করে আপনার ইনপুট যাচাই করুন (Bad Request).';
  }
  if (status === 403) {
    return 'আপনার এই কাজের অনুমতি নেই — এই আবেদনটি অন্য শিক্ষার্থীর (Forbidden).';
  }
  if (status === 404) {
    return 'চাহিদাকৃত তথ্য পাওয়া যায়নি (Not Found).';
  }
  if (status === 409) {
    return detail
      ? `অনুরোধটি সম্পন্ন করা যায়নি (409): ${detail}`
      : 'অনুরোধটি সম্পন্ন করা যায়নি — খালি seat নেই অথবা আবেদনটি আর সক্রিয় নেই (Conflict).';
  }
  if (status >= 500) {
    return 'সার্ভারে ত্রুটি ঘটেছে, কিছুক্ষণ পর আবার চেষ্টা করুন (Internal server error).';
  }

  return detail || `অনুরোধটি ব্যর্থ হয়েছে (HTTP ${status}).`;
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  studentId?: string;
}

async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
  };

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (options.studentId !== undefined && options.studentId.trim() !== '') {
    headers['X-Student-Id'] = options.studentId.trim();
  }

  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body:
        options.body !== undefined ? JSON.stringify(options.body) : undefined,
      signal: AbortSignal.timeout(10_000),
    });
  } catch {
    throw new ApiError(
      0,
      'NETWORK_ERROR',
      'Server-এর সাথে যোগাযোগ করা যাচ্ছে না (Server unreachable)',
      undefined,
      true,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let parsed: unknown = undefined;
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      parsed = undefined;
    }
  }

  if (!response.ok) {
    const problem = (
      parsed && typeof parsed === 'object' ? parsed : {}
    ) as ProblemDetailsPayload;
    const code =
      typeof problem.code === 'string' && problem.code.trim() !== ''
        ? problem.code
        : undefined;
    const detail =
      typeof problem.detail === 'string' && problem.detail.trim() !== ''
        ? problem.detail
        : typeof problem.message === 'string' && problem.message.trim() !== ''
          ? problem.message
          : typeof problem.title === 'string' && problem.title.trim() !== ''
            ? problem.title
            : undefined;

    const friendlyMessage = mapErrorMessage(response.status, code, detail);
    throw new ApiError(
      response.status,
      code ?? `HTTP_${response.status}`,
      friendlyMessage,
      detail,
      false,
    );
  }

  if (parsed === undefined) {
    throw new ApiError(
      response.status,
      'INVALID_RESPONSE',
      'সার্ভার থেকে অপ্রত্যাশিত ফরম্যাটে উত্তর এসেছে (Invalid response format from server).',
    );
  }

  return parsed as T;
}

/** 1. POST /classes */
export function createClass(
  payload: CreateClassRequest,
): Promise<ClassCreatedResponse> {
  return apiRequest<ClassCreatedResponse>('/classes', {
    method: 'POST',
    body: payload,
  });
}

/** 2. GET /classes */
export function getClasses(): Promise<ClassSeatsResponse[]> {
  return apiRequest<ClassSeatsResponse[]>('/classes');
}

/** 3. GET /classes/{classId} */
export function getClassById(classId: string): Promise<ClassSeatsResponse> {
  return apiRequest<ClassSeatsResponse>(
    `/classes/${encodeURIComponent(classId)}`,
  );
}

/** 4. POST /classes/{classId}/holds */
export function createHold(
  classId: string,
  studentId: string,
): Promise<HoldCreatedResponse> {
  return apiRequest<HoldCreatedResponse>(
    `/classes/${encodeURIComponent(classId)}/holds`,
    {
      method: 'POST',
      studentId,
    },
  );
}

/** 5. GET /holds/{holdId} */
export function getHoldById(holdId: string): Promise<HoldResponse> {
  return apiRequest<HoldResponse>(`/holds/${encodeURIComponent(holdId)}`);
}

/** 6. POST /holds/{holdId}/confirm */
export function confirmHold(
  holdId: string,
  studentId: string,
): Promise<HoldConfirmedResponse> {
  return apiRequest<HoldConfirmedResponse>(
    `/holds/${encodeURIComponent(holdId)}/confirm`,
    {
      method: 'POST',
      studentId,
    },
  );
}

/** 7. DELETE /holds/{holdId} */
export function cancelHold(holdId: string, studentId: string): Promise<void> {
  return apiRequest<void>(`/holds/${encodeURIComponent(holdId)}`, {
    method: 'DELETE',
    studentId,
  });
}

/** 8. GET /me/holds */
export function getMyHolds(studentId: string): Promise<MyHoldResponse[]> {
  return apiRequest<MyHoldResponse[]>('/me/holds', {
    method: 'GET',
    studentId,
  });
}

