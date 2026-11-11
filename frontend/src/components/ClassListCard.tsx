import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { ApiError, createHold, getClasses, isValidStudentId } from '../api';
import { saveStoredSelectedHoldId } from '../storage';
import type { HoldCreatedResponse } from '../types';

interface ClassListCardProps {
  studentId: string;
  onHoldCreated: (hold: HoldCreatedResponse) => void;
  onNetworkError?: (message: string | null) => void;
}

export function ClassListCard({
  studentId,
  onHoldCreated,
  onNetworkError,
}: ClassListCardProps) {
  const queryClient = useQueryClient();
  const validStudent = isValidStudentId(studentId);
  const [applyFeedback, setApplyFeedback] = useState<{
    type: 'success' | 'danger';
    message: string;
  } | null>(null);
  const [applyingClassId, setApplyingClassId] = useState<string | null>(null);

  const classesQuery = useQuery({
    queryKey: ['classes'],
    queryFn: getClasses,
    refetchInterval: 3000,
  });

  useEffect(() => {
    if (classesQuery.isSuccess) {
      onNetworkError?.(null);
    } else if (
      classesQuery.error instanceof ApiError &&
      classesQuery.error.isNetworkError
    ) {
      onNetworkError?.(classesQuery.error.message);
    }
  }, [classesQuery.isSuccess, classesQuery.error, onNetworkError]);

  const applyMutation = useMutation({
    mutationFn: (classId: string) => {
      setApplyingClassId(classId);
      return createHold(classId, studentId.trim());
    },
    onSuccess: async (createdHold) => {
      setApplyingClassId(null);
      saveStoredSelectedHoldId(studentId, createdHold.holdId);
      onHoldCreated(createdHold);
      onNetworkError?.(null);
      setApplyFeedback({
        type: 'success',
        message:
          'আবেদন সফলভাবে গৃহীত হয়েছে এবং আপনার নামে আসন সাময়িকভাবে সংরক্ষিত (HELD) হয়েছে। অনুগ্রহ করে নির্ধারিত সময়ের মধ্যে ভর্তি ফি জমা দিন।',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
        queryClient.invalidateQueries({ queryKey: ['hold'] }),
      ]);
    },
    onError: async (err) => {
      setApplyingClassId(null);
      if (err instanceof ApiError && err.isNetworkError) {
        onNetworkError?.(err.message);
      }
      setApplyFeedback({
        type: 'danger',
        message:
          err instanceof Error
            ? err.message
            : 'আবেদন করতে সমস্যা হয়েছে (Failed to apply).',
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-holds'] }),
      ]);
    },
  });

  const classes = classesQuery.data ?? [];

  return (
    <section className="card shadow-sm h-100">
      <div className="card-header bg-white py-3 d-flex justify-content-between align-items-center">
        <h2 className="h5 mb-0 fw-bold">Class তালিকা</h2>
        <span className="badge bg-secondary-subtle text-secondary-emphasis fw-normal">
          স্বয়ংক্রিয় হালনাগাদ (প্রতি ৩ সেকেন্ডে)
        </span>
      </div>

      <div className="card-body">
        {!validStudent && (
          <div className="alert alert-info py-2 small mb-3">
            আবেদন করার জন্য উপরে ১৭ অঙ্কের সঠিক জন্ম নিবন্ধন নম্বর লিখুন।
          </div>
        )}

        {applyFeedback && (
          <div
            className={`alert alert-${applyFeedback.type} alert-dismissible py-2 small mb-3`}
            role="alert"
          >
            <span>{applyFeedback.message}</span>
            <button
              type="button"
              className="btn-close py-2"
              aria-label="Close"
              onClick={() => setApplyFeedback(null)}
            />
          </div>
        )}

        {classesQuery.isLoading && (
          <div className="text-muted py-3">
            Class তালিকা লোড হচ্ছে... (Loading classes...)
          </div>
        )}

        {classesQuery.isError && !classesQuery.data && (
          <div className="alert alert-danger py-2 small mb-0" role="alert">
            {classesQuery.error instanceof Error
              ? classesQuery.error.message
              : 'Class তালিকা লোড করা যায়নি।'}
          </div>
        )}

        {!classesQuery.isLoading && classes.length === 0 && (
          <div className="text-muted py-3">
            এখনো কোনো Class তৈরি করা হয়নি। বাম পাশের &ldquo;Class তৈরি&rdquo; ফর্ম থেকে নতুন Class যোগ করুন।
          </div>
        )}

        {classes.length > 0 && (
          <div className="table-responsive">
            <table className="table table-hover align-middle mb-0">
              <thead className="table-light">
                <tr>
                  <th>Class / Shift</th>
                  <th className="text-center">মোট</th>
                  <th className="text-center">Hold-এ থাকা</th>
                  <th className="text-center">নিশ্চিত</th>
                  <th className="text-center">খালি</th>
                  <th className="text-end">কাজ</th>
                </tr>
              </thead>
              <tbody>
                {classes.map((cls) => {
                  const isApplyingThis =
                    applyMutation.isPending && applyingClassId === cls.id;
                  const disableApply =
                    !validStudent ||
                    cls.availableSeats === 0 ||
                    applyMutation.isPending;

                  return (
                    <tr key={cls.id}>
                      <td className="fw-semibold">{cls.name}</td>
                      <td className="text-center">{cls.totalSeats}</td>
                      <td className="text-center">
                        <span className="badge bg-warning-subtle text-warning-emphasis">
                          {cls.heldSeats}
                        </span>
                      </td>
                      <td className="text-center">
                        <span className="badge bg-success-subtle text-success-emphasis">
                          {cls.confirmedSeats}
                        </span>
                      </td>
                      <td className="text-center">
                        <span
                          className={`badge ${
                            cls.availableSeats > 0
                              ? 'bg-primary-subtle text-primary-emphasis'
                              : 'bg-danger-subtle text-danger-emphasis'
                          }`}
                        >
                          {cls.availableSeats}
                        </span>
                      </td>
                      <td className="text-end">
                        <button
                          type="button"
                          className="btn btn-sm btn-primary"
                          disabled={disableApply}
                          onClick={() => {
                            setApplyFeedback(null);
                            applyMutation.mutate(cls.id);
                          }}
                        >
                          {isApplyingThis
                            ? 'আবেদন হচ্ছে...'
                            : cls.availableSeats === 0
                              ? 'আসন খালি নেই'
                              : 'আবেদন করুন'}
                        </button>
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
