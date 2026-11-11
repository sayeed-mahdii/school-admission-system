const STUDENT_ID_STORAGE_KEY = 'admission_student_id';
const SELECTED_HOLD_PREFIX = 'admission_selected_hold_';

export function loadStoredStudentId(): string {
  try {
    return localStorage.getItem(STUDENT_ID_STORAGE_KEY)?.trim() ?? '';
  } catch {
    return '';
  }
}

export function saveStoredStudentId(studentId: string): void {
  try {
    const trimmed = studentId.trim();
    if (trimmed) {
      localStorage.setItem(STUDENT_ID_STORAGE_KEY, trimmed);
    } else {
      localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
    }
  } catch {
    // Ignore storage errors
  }
}

export function loadStoredSelectedHoldId(studentId: string): string | null {
  const trimmed = studentId.trim();
  if (!trimmed) return null;
  try {
    return localStorage.getItem(`${SELECTED_HOLD_PREFIX}${trimmed}`);
  } catch {
    return null;
  }
}

export function saveStoredSelectedHoldId(
  studentId: string,
  holdId: string | null,
): void {
  const trimmed = studentId.trim();
  if (!trimmed) return;
  try {
    if (holdId) {
      localStorage.setItem(`${SELECTED_HOLD_PREFIX}${trimmed}`, holdId);
    } else {
      localStorage.removeItem(`${SELECTED_HOLD_PREFIX}${trimmed}`);
    }
  } catch {
    // Ignore storage errors
  }
}
