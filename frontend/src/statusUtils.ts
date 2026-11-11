import { HOLD_STATUS, type HoldStatus, type MyHoldResponse } from './types';

export function computeClockOffsetMs(
  serverTime: string | undefined,
  dataUpdatedAt: number,
): number {
  if (!serverTime || !dataUpdatedAt) {
    return 0;
  }
  const serverMs = new Date(serverTime).getTime();
  if (Number.isNaN(serverMs)) {
    return 0;
  }
  return serverMs - dataUpdatedAt;
}

export function computeRemainingMs(
  expiresAt: string,
  nowMs: number,
  offsetMs: number,
): number {
  const expiresMs = new Date(expiresAt).getTime();
  if (Number.isNaN(expiresMs)) {
    return 0;
  }
  const adjustedServerNow = nowMs + offsetMs;
  return expiresMs - adjustedServerNow;
}

export function deriveDisplayStatus(
  serverStatus: HoldStatus,
  remainingMs: number,
): HoldStatus {
  if (serverStatus === HOLD_STATUS.HELD && remainingMs <= 0) {
    return HOLD_STATUS.EXPIRED;
  }
  return serverStatus;
}

export function formatCountdown(remainingMs: number): string {
  const totalSeconds = Math.max(0, Math.ceil(remainingMs / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function getStatusBadgeClass(status: HoldStatus): string {
  switch (status) {
    case HOLD_STATUS.HELD:
      return 'bg-warning text-dark';
    case HOLD_STATUS.CONFIRMED:
      return 'bg-success text-white';
    case HOLD_STATUS.EXPIRED:
      return 'bg-secondary text-white';
    case HOLD_STATUS.CANCELLED:
      return 'bg-danger text-white';
  }
}

export function getStatusLabel(status: HoldStatus): string {
  switch (status) {
    case HOLD_STATUS.HELD:
      return 'HELD (সাময়িক সংরক্ষিত)';
    case HOLD_STATUS.CONFIRMED:
      return 'CONFIRMED (ভর্তি নিশ্চিত)';
    case HOLD_STATUS.EXPIRED:
      return 'EXPIRED (মেয়াদোত্তীর্ণ)';
    case HOLD_STATUS.CANCELLED:
      return 'CANCELLED (বাতিলকৃত)';
  }
}

export function resolveEffectiveHoldId(
  selectedHoldId: string | null,
  myHolds: MyHoldResponse[] | undefined,
): string | null {
  if (myHolds === undefined) {
    return selectedHoldId;
  }
  if (myHolds.length === 0) {
    return null;
  }
  if (selectedHoldId && myHolds.some((h) => h.holdId === selectedHoldId)) {
    return selectedHoldId;
  }
  const latestHeld = myHolds.find((h) => h.status === HOLD_STATUS.HELD);
  return (latestHeld ?? myHolds[0]).holdId;
}
