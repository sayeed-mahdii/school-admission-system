import { useMutation, useQueryClient } from '@tanstack/react-query';
import { type FormEvent, useState } from 'react';
import { ApiError, createClass } from '../api';

interface CreateClassCardProps {
  onNetworkError?: (message: string | null) => void;
}

export function CreateClassCard({ onNetworkError }: CreateClassCardProps) {
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [totalSeatsInput, setTotalSeatsInput] = useState('120');
  const [validationError, setValidationError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: createClass,
    onSuccess: async (created) => {
      setName('');
      setTotalSeatsInput('120');
      setValidationError(null);
      setSuccessMessage(
        `"${created.name}" সফলভাবে তৈরি হয়েছে (${created.totalSeats}টি আসন)।`,
      );
      onNetworkError?.(null);
      await queryClient.invalidateQueries({ queryKey: ['classes'] });
    },
    onError: (err) => {
      setSuccessMessage(null);
      if (err instanceof ApiError && err.isNetworkError) {
        onNetworkError?.(err.message);
      }
    },
  });

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setSuccessMessage(null);
    mutation.reset();

    const trimmedName = name.trim();
    if (!trimmedName) {
      setValidationError('Class-এর নাম লিখুন (Class name is required).');
      return;
    }
    if (trimmedName.length > 200) {
      setValidationError(
        'Class-এর নাম সর্বোচ্চ ২০০ অক্ষরের হতে পারে (Max 200 characters).',
      );
      return;
    }

    const parsedSeats = Number(totalSeatsInput);
    if (
      !Number.isInteger(parsedSeats) ||
      parsedSeats < 1 ||
      parsedSeats > 100000
    ) {
      setValidationError(
        'আসন সংখ্যা কমপক্ষে ১ (পূর্ণসংখ্যা, সর্বোচ্চ ১০০,০০০) হতে হবে (Total seats must be an integer >= 1).',
      );
      return;
    }

    setValidationError(null);
    mutation.mutate({
      name: trimmedName,
      totalSeats: parsedSeats,
    });
  };

  const apiErrorMessage =
    mutation.error instanceof Error
      ? mutation.error.message
      : mutation.isError
        ? 'Class তৈরি করতে সমস্যা হয়েছে।'
        : null;

  return (
    <section className="card shadow-sm h-100">
      <div className="card-header bg-white py-3">
        <h2 className="h5 mb-0 fw-bold">Class তৈরি (school admin)</h2>
      </div>
      <div className="card-body">
        <form onSubmit={handleSubmit} noValidate>
          <div className="mb-3">
            <label htmlFor="class-name-input" className="form-label fw-medium">
              Class ও Shift-এর নাম
            </label>
            <input
              id="class-name-input"
              type="text"
              className="form-control"
              placeholder="যেমন: Class 6 - Morning Shift"
              maxLength={200}
              value={name}
              disabled={mutation.isPending}
              onChange={(e) => {
                setName(e.target.value);
                if (validationError) setValidationError(null);
                if (successMessage) setSuccessMessage(null);
              }}
            />
          </div>

          <div className="mb-3">
            <label htmlFor="class-seats-input" className="form-label fw-medium">
              মোট আসন সংখ্যা (Total Seats)
            </label>
            <input
              id="class-seats-input"
              type="number"
              min={1}
              max={100000}
              step={1}
              className="form-control"
              placeholder="যেমন: 120"
              value={totalSeatsInput}
              disabled={mutation.isPending}
              onChange={(e) => {
                setTotalSeatsInput(e.target.value);
                if (validationError) setValidationError(null);
                if (successMessage) setSuccessMessage(null);
              }}
            />
          </div>

          {validationError && (
            <div className="alert alert-warning py-2 small mb-3" role="alert">
              {validationError}
            </div>
          )}

          {apiErrorMessage && (
            <div className="alert alert-danger py-2 small mb-3" role="alert">
              {apiErrorMessage}
            </div>
          )}

          {successMessage && (
            <div className="alert alert-success py-2 small mb-3" role="status">
              {successMessage}
            </div>
          )}

          <button
            type="submit"
            className="btn btn-primary w-100"
            disabled={mutation.isPending}
          >
            {mutation.isPending
              ? 'তৈরি হচ্ছে... (Creating...)'
              : 'নতুন Class তৈরি করুন'}
          </button>
        </form>
      </div>
    </section>
  );
}
