import { useEffect, useState } from 'react';
import { isValidStudentId } from './api';
import { ClassListCard } from './components/ClassListCard';
import { CreateClassCard } from './components/CreateClassCard';
import { StudentIdBar } from './components/StudentIdBar';
import {
  loadStoredSelectedHoldId,
  loadStoredStudentId,
  saveStoredSelectedHoldId,
  saveStoredStudentId,
} from './storage';

function App() {
  const [studentId, setStudentId] = useState<string>(loadStoredStudentId);
  const [selectedHoldId, setSelectedHoldId] = useState<string | null>(() =>
    loadStoredSelectedHoldId(loadStoredStudentId()),
  );
  const [globalError, setGlobalError] = useState<string | null>(null);

  useEffect(() => {
    saveStoredStudentId(studentId);
  }, [studentId]);

  const handleStudentIdChange = (nextStudentId: string) => {
    setStudentId(nextStudentId);
    setSelectedHoldId(loadStoredSelectedHoldId(nextStudentId));
  };

  const handleSelectHold = (holdId: string | null) => {
    setSelectedHoldId(holdId);
    saveStoredSelectedHoldId(studentId, holdId);
  };

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

        <StudentIdBar
          studentId={studentId}
          onChange={handleStudentIdChange}
        />

        <div className="row g-4">
          {/* 1. Class তৈরি (school admin) */}
          <div className="col-12 col-lg-5">
            <CreateClassCard onNetworkError={setGlobalError} />
          </div>

          {/* 2. Class তালিকা */}
          <div className="col-12 col-lg-7">
            <ClassListCard
              studentId={studentId}
              onHoldCreated={(hold) => handleSelectHold(hold.holdId)}
              onNetworkError={setGlobalError}
            />
          </div>

          {/* 3. আবেদন */}
          <div className="col-12 col-lg-6">
            <section className="card shadow-sm h-100">
              <div className="card-header bg-white py-3">
                <h2 className="h5 mb-0 fw-bold">আবেদন</h2>
              </div>
              <div className="card-body text-muted">
                {selectedHoldId
                  ? `নির্বাচিত আবেদন ID: ${selectedHoldId}`
                  : 'নির্বাচিত আবেদনের বর্তমান অবস্থা, সময়সীমা, ভর্তি ফি জমা এবং বাতিল।'}
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
