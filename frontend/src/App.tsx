import { useEffect, useState } from 'react';
import { isValidStudentId } from './api';
import { StudentIdBar } from './components/StudentIdBar';

const STUDENT_ID_STORAGE_KEY = 'admission_student_id';

function loadInitialStudentId(): string {
  try {
    return localStorage.getItem(STUDENT_ID_STORAGE_KEY)?.trim() ?? '';
  } catch {
    return '';
  }
}

function App() {
  const [studentId, setStudentId] = useState<string>(loadInitialStudentId);
  const [globalError, setGlobalError] = useState<string | null>(null);

  useEffect(() => {
    try {
      const trimmed = studentId.trim();
      if (trimmed) {
        localStorage.setItem(STUDENT_ID_STORAGE_KEY, trimmed);
      } else {
        localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
      }
    } catch {
      // Ignore localStorage quota or privacy errors
    }
  }, [studentId]);

  const validStudent = isValidStudentId(studentId);

  return (
    <div className="min-vh-100 bg-light pb-5">
      <header className="bg-primary text-white py-3 mb-4 shadow-sm">
        <div className="container d-flex flex-wrap justify-content-between align-items-center gap-2">
          <div>
            <h1 className="h4 mb-0 fw-bold">School Admission System</h1>
            <small className="opacity-75">
              অনলাইন ভর্তি ব্যবস্থাপনা ( আগে আবেদন করলে আগে পাবে )
            </small>
          </div>
          {validStudent && (
            <span className="badge bg-light text-primary font-monospace fs-6">
              ID: {studentId.trim()}
            </span>
          )}
        </div>
      </header>

      <main className="container">
        {globalError && (
          <div
            className="alert alert-danger alert-dismissible fade show shadow-sm mb-4"
            role="alert"
          >
            <strong>ত্রুটি: </strong>
            <span>{globalError}</span>
            <button
              type="button"
              className="btn-close"
              aria-label="Close"
              onClick={() => setGlobalError(null)}
            />
          </div>
        )}

        <StudentIdBar studentId={studentId} onChange={setStudentId} />

        <div className="row g-4">
          {/* 1. Class তৈরি (school admin) */}
          <div className="col-12 col-lg-5">
            <section className="card shadow-sm h-100">
              <div className="card-header bg-white py-3">
                <h2 className="h5 mb-0 fw-bold">Class তৈরি (school admin)</h2>
              </div>
              <div className="card-body text-muted">
                নতুন class এবং seat সংখ্যা যোগ করার ফর্ম।
              </div>
            </section>
          </div>

          {/* 2. Class তালিকা */}
          <div className="col-12 col-lg-7">
            <section className="card shadow-sm h-100">
              <div className="card-header bg-white py-3">
                <h2 className="h5 mb-0 fw-bold">Class তালিকা</h2>
              </div>
              <div className="card-body text-muted">
                সকল class-এর মোট, Hold-এ থাকা, নিশ্চিত ও খালি seat-এর তালিকা।
              </div>
            </section>
          </div>

          {/* 3. আবেদন */}
          <div className="col-12 col-lg-6">
            <section className="card shadow-sm h-100">
              <div className="card-header bg-white py-3">
                <h2 className="h5 mb-0 fw-bold">আবেদন</h2>
              </div>
              <div className="card-body text-muted">
                নির্বাচিত আবেদনের বর্তমান অবস্থা, সময়সীমা, ভর্তি ফি জমা এবং বাতিল।
              </div>
            </section>
          </div>

          {/* 4. আমার আবেদন */}
          <div className="col-12 col-lg-6">
            <section className="card shadow-sm h-100">
              <div className="card-header bg-white py-3">
                <h2 className="h5 mb-0 fw-bold">আমার আবেদন</h2>
              </div>
              <div className="card-body text-muted">
                এই শিক্ষার্থীর সকল আবেদনের তালিকা এবং বর্তমান অবস্থা।
              </div>
            </section>
          </div>
        </div>
      </main>
    </div>
  );
}

export default App;
