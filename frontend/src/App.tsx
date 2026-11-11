import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { ApiError, getMyHolds, isValidStudentId } from './api';
import { ApplicationCard } from './components/ApplicationCard';
import { ClassListCard } from './components/ClassListCard';
import { CreateClassCard } from './components/CreateClassCard';
import { StudentIdBar } from './components/StudentIdBar';
import { resolveEffectiveHoldId } from './statusUtils';
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

  const validStudent = isValidStudentId(studentId);
  const trimmedStudentId = studentId.trim();

  const myHoldsQuery = useQuery({
    queryKey: ['my-holds', trimmedStudentId],
    queryFn: () => getMyHolds(trimmedStudentId),
    enabled: validStudent,
    refetchInterval: 3000,
  });

  const myHoldsNetworkError =
    myHoldsQuery.error instanceof ApiError && myHoldsQuery.error.isNetworkError
      ? myHoldsQuery.error.message
      : null;

  const displayedGlobalError = globalError ?? myHoldsNetworkError;

  const effectiveHoldId = validStudent
    ? resolveEffectiveHoldId(selectedHoldId, myHoldsQuery.data)
    : null;

  useEffect(() => {
    saveStoredStudentId(studentId);
  }, [studentId]);

  useEffect(() => {
    if (validStudent && effectiveHoldId) {
      saveStoredSelectedHoldId(studentId, effectiveHoldId);
    }
  }, [validStudent, studentId, effectiveHoldId]);

  const handleStudentIdChange = (nextStudentId: string) => {
    setStudentId(nextStudentId);
    setSelectedHoldId(loadStoredSelectedHoldId(nextStudentId));
  };

  const handleSelectHold = (holdId: string | null) => {
    setSelectedHoldId(holdId);
    saveStoredSelectedHoldId(studentId, holdId);
  };

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
              ID: {trimmedStudentId}
            </span>
          )}
        </div>
      </header>

      <main className="container">
        {displayedGlobalError && (
          <div
            className="alert alert-danger alert-dismissible fade show shadow-sm mb-4"
            role="alert"
          >
            <strong>ত্রুটি: </strong>
            <span>{displayedGlobalError}</span>
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
            <ApplicationCard
              studentId={studentId}
              holdId={effectiveHoldId}
              myHolds={myHoldsQuery.data ?? []}
              myHoldsDataUpdatedAt={myHoldsQuery.dataUpdatedAt}
              onNetworkError={setGlobalError}
            />
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
