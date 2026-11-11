import { useEffect, useState } from 'react';
import { ApiError, isValidStudentId } from '../api';
import {
  computeClockOffsetMs,
  computeRemainingMs,
  deriveDisplayStatus,
  formatCountdown,
  getStatusBadgeClass,
  getStatusLabel,
} from '../statusUtils';
import { HOLD_STATUS, type MyHoldResponse } from '../types';

interface MyApplicationsCardProps {
  studentId: string;
  holds: MyHoldResponse[];
  isLoading: boolean;
  isSuccess: boolean;
  error: Error | null;
  dataUpdatedAt: number;
  selectedHoldId: string | null;
  onSelectHold: (holdId: string) => void;
  onNetworkError?: (message: string | null) => void;
}

export function MyApplicationsCard({
  studentId,
  holds,
  isLoading,
  isSuccess,
  error,
  dataUpdatedAt,
  selectedHoldId,
  onSelectHold,
  onNetworkError,
}: MyApplicationsCardProps) {
  const validStudent = isValidStudentId(studentId);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());

  useEffect(() => {
    if (isSuccess) {
      onNetworkError?.(null);
    } else if (error instanceof ApiError && error.isNetworkError) {
      onNetworkError?.(error.message);
    }
  }, [isSuccess, error, onNetworkError]);

  useEffect(() => {
    if (holds.length === 0) return undefined;
    const timer = window.setInterval(() => {
      setNowMs(Date.now());
    }, 500);
    return () => window.clearInterval(timer);
  }, [holds.length]);

  return (
    <section className="card shadow-sm h-100">
      <div className="card-header bg-white py-3 d-flex justify-content-between align-items-center">
        <h2 className="h5 mb-0 fw-bold">আমার আবেদন</h2>
        {validStudent && holds.length > 0 && (
          <span className="badge bg-primary-subtle text-primary-emphasis">
            মোট: {holds.length}
          </span>
        )}
      </div>

      <div className="card-body">
        {!validStudent && (
          <div className="text-muted py-3">
            আপনার সকল আবেদনের তালিকা দেখতে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।
          </div>
        )}

        {validStudent && isLoading && holds.length === 0 && (
          <div className="text-muted py-3">
            আপনার আবেদনসমূহ লোড হচ্ছে... (Loading your applications...)
          </div>
        )}

        {validStudent && error && holds.length === 0 && (
          <div className="alert alert-danger py-2 small mb-0" role="alert">
            {error.message}
          </div>
        )}

        {validStudent && !isLoading && !error && holds.length === 0 && (
          <div className="text-muted py-3">
            এই জন্ম নিবন্ধন নম্বরের বিপরীতে এখনো কোনো আবেদন পাওয়া যায়নি।
          </div>
        )}

        {validStudent && holds.length > 0 && (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th>Class / Shift</th>
                  <th className="text-center">অবস্থা (Status)</th>
                  <th className="text-end">মেয়াদ শেষের সময়</th>
                </tr>
              </thead>
              <tbody>
                {holds.map((hold) => {
                  const offsetMs = computeClockOffsetMs(
                    hold.serverTime,
                    dataUpdatedAt,
                  );
                  const remainingMs = computeRemainingMs(
                    hold.expiresAt,
                    nowMs,
                    offsetMs,
                  );
                  const displayStatus = deriveDisplayStatus(
                    hold.status,
                    remainingMs,
                  );
                  const isSelected = hold.holdId === selectedHoldId;

                  return (
                    <tr
                      key={hold.holdId}
                      role="button"
                      tabIndex={0}
                      className={isSelected ? 'table-primary' : ''}
                      style={{ cursor: 'pointer' }}
                      onClick={() => onSelectHold(hold.holdId)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          onSelectHold(hold.holdId);
                        }
                      }}
                    >
                      <td>
                        <div className="fw-semibold">{hold.className}</div>
                        <small className="text-muted font-monospace">
                          {hold.holdId.slice(0, 8)}...
                        </small>
                      </td>
                      <td className="text-center">
                        <span
                          className={`badge ${getStatusBadgeClass(displayStatus)}`}
                        >
                          {getStatusLabel(displayStatus)}
                        </span>
                        {displayStatus === HOLD_STATUS.HELD && (
                          <div className="small font-monospace text-primary fw-semibold mt-1">
                            {formatCountdown(remainingMs)}
                          </div>
                        )}
                      </td>
                      <td className="text-end small">
                        <div>
                          {new Date(hold.expiresAt).toLocaleTimeString([], {
                            hour: '2-digit',
                            minute: '2-digit',
                            second: '2-digit',
                          })}
                        </div>
                        <span className="text-primary small">
                          {isSelected ? 'নির্বাচিত' : 'দেখুন →'}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
