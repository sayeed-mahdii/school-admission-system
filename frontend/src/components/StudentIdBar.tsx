import { isValidStudentId } from '../api';

interface StudentIdBarProps {
  studentId: string;
  onChange: (nextStudentId: string) => void;
}

export function StudentIdBar({ studentId, onChange }: StudentIdBarProps) {
  const trimmed = studentId.trim();
  const isValid = isValidStudentId(trimmed);
  const hasInput = trimmed.length > 0;

  return (
    <div className="card border-primary shadow-sm mb-4">
      <div className="card-body">
        <div className="row g-3 align-items-center">
          <div className="col-12 col-md-5">
            <label
              htmlFor="student-birth-reg-input"
              className="form-label fw-semibold mb-1"
            >
              শিক্ষার্থীর জন্ম নিবন্ধন নম্বর
            </label>
            <div className="text-muted small">
              ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর (17-digit Birth Registration Number)
            </div>
          </div>

          <div className="col-12 col-md-7">
            <div className="input-group">
              <input
                id="student-birth-reg-input"
                type="text"
                inputMode="numeric"
                maxLength={17}
                className={`form-control font-monospace ${
                  hasInput ? (isValid ? 'is-valid' : 'is-invalid') : ''
                }`}
                placeholder="যেমন: 20152691234567890"
                value={studentId}
                onChange={(e) => onChange(e.target.value.trim())}
                aria-describedby="student-id-hint"
              />
              <span className="input-group-text font-monospace">
                {trimmed.length}/17
              </span>
            </div>

            <div
              id="student-id-hint"
              className={`form-text mt-1 ${
                !hasInput
                  ? 'text-muted'
                  : isValid
                    ? 'text-success fw-medium'
                    : 'text-danger fw-medium'
              }`}
            >
              {!hasInput &&
                'আবেদন করতে বা নিজের আবেদন দেখতে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর লিখুন।'}
              {hasInput &&
                isValid &&
                'সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দেওয়া হয়েছে।'}
              {hasInput &&
                !isValid &&
                'জন্ম নিবন্ধন নম্বর অবশ্যই ঠিক ১৭ অঙ্কের ইংরেজি সংখ্যা (0-9) হতে হবে।'}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

