import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import {
  ApiError,
  cancelHold,
  confirmHold,
  getHoldById,
  isValidStudentId,
} from '../api';
import {
  computeClockOffsetMs,
  computeRemainingMs,
  deriveDisplayStatus,
  formatCountdown,
  getStatusBadgeClass,
  getStatusLabel,
} from '../statusUtils';
import { HOLD_STATUS, type MyHoldResponse } from '../types';

interface ApplicationCardProps {
  studentId: string;
  holdId: string | null;
  myHolds: MyHoldResponse[];
  myHoldsDataUpdatedAt: number;
  onNetworkError?: (message: string | null) => void;
}

export function ApplicationCard({
  studentId,
  holdId,
  myHolds,
  myHoldsDataUpdatedAt,
  onNetworkError,
}: ApplicationCardProps) {
  const queryClient = useQueryClient();
  const validStudent = isValidStudentId(studentId);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());
  const [feedback, setFeedback] = useState<{
    holdId: string;
    type: 'success' | 'danger';
    message: string;
  } | null>(null);
  const expiredNotifiedRef = useRef<string | null>(null);

  const matchingMyHold = myHolds.find((h) => h.holdId === holdId);

  const holdQuery = useQuery({
    queryKey: ['hold', holdId],
    queryFn: () => getHoldById(holdId!),
    enabled: Boolean(holdId),
    refetchInterval: 3000,
  });

  useEffect(() => {
    if (holdQuery.isSuccess) {
      onNetworkError?.(null);
    } else if (
      holdQuery.error instanceof ApiError &&
      holdQuery.error.isNetworkError
    ) {
      onNetworkError?.(holdQuery.error.message);
    }
  }, [holdQuery.isSuccess, holdQuery.error, onNetworkError]);

  const activeHold = holdQuery.data ?? matchingMyHold ?? null;
  const offsetMs = computeClockOffsetMs(
    holdQuery.data?.serverTime ?? matchingMyHold?.serverTime,
    holdQuery.dataUpdatedAt || myHoldsDataUpdatedAt,
  );

  useEffect(() => {
    if (!activeHold) return undefined;

    const timer = window.setInterval(() => {
      const currentNow = Date.now();
      setNowMs(currentNow);

      if (activeHold.status === HOLD_STATUS.HELD) {
        const rem = computeRemainingMs(
          activeHold.expiresAt,
          currentNow,
          offsetMs,
        );
        if (rem <= 0 && expiredNotifiedRef.current !== activeHold.holdId) {
          expiredNotifiedRef.current = activeHold.holdId;
          void Promise.all([
            queryClient.invalidateQueries({ queryKey: ['classes'] }),
            queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
            queryClient.invalidateQueries({ queryKey: ['hold'] }),
          ]);
        }
      }
    }, 250);

    return () => window.clearInterval(timer);
  }, [activeHold, offsetMs, queryClient]);

  const confirmMutation = useMutation({
    mutationFn: (targetHoldId: string) =>
      confirmHold(targetHoldId, studentId.trim()),
    onSuccess: async (_data, targetHoldId) => {
      onNetworkError?.(null);
      setFeedback({
        holdId: targetHoldId,
        type: 'success',
        message:
          'ভর্তি ফি সফলভাবে জমা হয়েছে এবং আপনার ভর্তি নিশ্চিত (CONFIRMED) হয়েছে!',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
        queryClient.invalidateQueries({ queryKey: ['hold'] }),
      ]);
    },
    onError: async (err, targetHoldId) => {
      if (err instanceof ApiError && err.isNetworkError) {
        onNetworkError?.(err.message);
      }
      setFeedback({
        holdId: targetHoldId,
        type: 'danger',
        message:
          err instanceof Error
            ? err.message
            : 'ভর্তি নিশ্চিত করতে সমস্যা হয়েছে।',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
        queryClient.invalidateQueries({ queryKey: ['hold'] }),
      ]);
    },
  });

  const cancelMutation = useMutation({
    mutationFn: (targetHoldId: string) =>
      cancelHold(targetHoldId, studentId.trim()),
    onSuccess: async (_data, targetHoldId) => {
      onNetworkError?.(null);
      setFeedback({
        holdId: targetHoldId,
        type: 'success',
        message:
          'আবেদনটি সফলভাবে বাতিল (CANCELLED) করা হয়েছে এবং আসনটি পুনরায় উন্মুক্ত হয়েছে।',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
        queryClient.invalidateQueries({ queryKey: ['hold'] }),
      ]);
    },
    onError: async (err, targetHoldId) => {
      if (err instanceof ApiError && err.isNetworkError) {
        onNetworkError?.(err.message);
      }
      setFeedback({
        holdId: targetHoldId,
        type: 'danger',
        message:
          err instanceof Error
            ? err.message
            : 'আবেদন বাতিল করতে সমস্যা হয়েছে।',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
        queryClient.invalidateQueries({ queryKey: ['hold'] }),
      ]);
    },
  });

  const remainingMs = activeHold
    ? computeRemainingMs(activeHold.expiresAt, nowMs, offsetMs)
    : 0;
  const displayStatus = activeHold
    ? deriveDisplayStatus(activeHold.status, remainingMs)
    : null;

  const isHeldAndActive = displayStatus === HOLD_STATUS.HELD && remainingMs > 0;
  const isBusy = confirmMutation.isPending || cancelMutation.isPending;
  const currentFeedback =
    feedback && feedback.holdId === holdId ? feedback : null;

  const classNameLabel =
    matchingMyHold?.className ??
    (activeHold ? `Class ID: ${activeHold.classId}` : '');

  return (
    <section className="card shadow-sm h-100">
      <div className="card-header bg-white py-3">
        <h2 className="h5 mb-0 fw-bold">আবেদন</h2>
      </div>

      <div className="card-body">
        {!validStudent && (
          <div className="text-muted py-3">
            আপনার চলমান বা পূর্ববর্তী আবেদনের বিস্তারিত দেখতে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।
          </div>
        )}

        {validStudent && !holdId && (
          <div className="text-muted py-3">
            এখনো কোনো আবেদন নির্বাচন করা হয়নি। উপরের &ldquo;Class তালিকা&rdquo; থেকে{' '}
            <strong>আবেদন করুন</strong> বাটনে চাপুন।
          </div>
        )}

        {validStudent && holdId && holdQuery.isLoading && !activeHold && (
          <div className="text-muted py-3">
            আবেদনের তথ্য লোড হচ্ছে... (Loading application...)
          </div>
        )}

        {validStudent && holdId && holdQuery.isError && !activeHold && (
          <div className="alert alert-danger py-2 small mb-0" role="alert">
            {holdQuery.error instanceof Error
              ? holdQuery.error.message
              : 'আবেদনের তথ্য পাওয়া যায়নি।'}
          </div>
        )}

        {validStudent && activeHold && displayStatus && (
          <div>
            {currentFeedback && (
              <div
                className={`alert alert-${currentFeedback.type} alert-dismissible py-2 small mb-3`}
                role="alert"
              >
                <span>{currentFeedback.message}</span>
                <button
                  type="button"
                  className="btn-close py-2"
                  aria-label="Close"
                  onClick={() => setFeedback(null)}
                />
              </div>
            )}

            <div className="border rounded p-3 mb-3 bg-light">
              <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                <div>
                  <div className="text-muted small">নির্বাচিত Class / Shift</div>
                  <div className="fw-bold fs-5">{classNameLabel}</div>
                </div>
                <span
                  className={`badge fs-6 ${getStatusBadgeClass(displayStatus)}`}
                >
                  {getStatusLabel(displayStatus)}
                </span>
              </div>

              <div className="row g-2 mt-2 pt-2 border-top small">
                <div className="col-12 col-sm-6">
                  <span className="text-muted">আবেদন নম্বর (Hold ID): </span>
                  <code className="text-dark">{activeHold.holdId}</code>
                </div>
                <div className="col-12 col-sm-6">
                  <span className="text-muted">মেয়াদ শেষের সময়: </span>
                  <span className="fw-medium">
                    {new Date(activeHold.expiresAt).toLocaleTimeString([], {
                      hour: '2-digit',
                      minute: '2-digit',
                      second: '2-digit',
                    })}
                  </span>
                </div>
              </div>

              <div className="mt-3 pt-3 border-top d-flex justify-content-between align-items-center">
                <span className="fw-semibold">
                  মেয়াদ শেষ হতে বাকি সময় (Time Remaining):
                </span>
                {displayStatus === HOLD_STATUS.HELD ? (
                  <span className="fs-4 fw-bold font-monospace text-primary">
                    {formatCountdown(remainingMs)}
                  </span>
                ) : displayStatus === HOLD_STATUS.CONFIRMED ? (
                  <span className="fw-bold text-success">
                    ভর্তি নিশ্চিত হয়েছে
                  </span>
                ) : displayStatus === HOLD_STATUS.EXPIRED ? (
                  <span className="fw-bold text-secondary">
                    00:00 (সময় শেষ)
                  </span>
                ) : (
                  <span className="fw-bold text-danger">আবেদন বাতিল</span>
                )}
              </div>
            </div>

            {displayStatus === HOLD_STATUS.EXPIRED && (
              <div
                className="alert alert-secondary py-2 small mb-3"
                role="status"
              >
                নির্ধারিত সময়ের মধ্যে ভর্তি ফি জমা না দেওয়ায় এই আবেদনের মেয়াদ শেষ (EXPIRED) হয়ে গেছে এবং আসনটি অন্যদের জন্য উন্মুক্ত হয়েছে। আপনি চাইলে পুনরায় &ldquo;Class তালিকা&rdquo; থেকে আবেদন করতে পারেন।
              </div>
            )}

            <div className="d-flex gap-2">
              <button
                type="button"
                className="btn btn-success flex-grow-1 fw-semibold"
                disabled={!isHeldAndActive || isBusy}
                onClick={() => {
                  setFeedback(null);
                  confirmMutation.mutate(activeHold.holdId);
                }}
              >
                {confirmMutation.isPending
                  ? 'ফি জমা হচ্ছে...'
                  : 'ভর্তি ফি দিন (Confirm Admission)'}
              </button>

              <button
                type="button"
                className="btn btn-outline-danger fw-semibold"
                disabled={!isHeldAndActive || isBusy}
                onClick={() => {
                  setFeedback(null);
                  cancelMutation.mutate(activeHold.holdId);
                }}
              >
                {cancelMutation.isPending ? 'বাতিল হচ্ছে...' : 'বাতিল (Cancel)'}
              </button>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
