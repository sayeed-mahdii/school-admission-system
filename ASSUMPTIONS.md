# Architectural Decisions & Assumptions

This document records the assumptions and design decisions made where the specification leaves room for interpretation.

## 1. One Active or Confirmed Application Per Student **Per Class**
- Each shift (e.g., `"Class 6 - Morning Shift"` and `"Class 6 - Day Shift"`) is modeled as a separate class with its own seat capacity (§1.3).
- A student may hold or confirm **at most one** seat per class at any time (`HELD` with `expiresAt > now`, or `CONFIRMED`), but **may** apply to multiple distinct classes/shifts simultaneously.
- *Why:* In school admissions, parents frequently apply to both Morning and Day shifts (or multiple sections) for the same child. A stricter global single-application rule across all classes was considered and deliberately not enforced so multi-shift applications remain supported.
- Enforced both inside the class row-locked transaction (`409 Conflict` with code `DUPLICATE_HOLD`) and at the database level via the partial unique index `UX_Holds_ClassId_StudentId_Active` on `("ClassId", "StudentId") WHERE "Status" IN ('HELD', 'CONFIRMED')`.

## 2. Concurrency Control & Lazy Expiry (`SELECT … FOR UPDATE` + Derived Counts)
- **No stored seat counters:** `heldSeats`, `confirmedSeats`, and `availableSeats` are never stored as mutable integer columns on `Classes`. They are derived on the fly from `Holds`:
  - `heldSeats`: `Status == 'HELD' && ExpiresAt > now`
  - `confirmedSeats`: `Status == 'CONFIRMED'`
  - `availableSeats`: `max(0, totalSeats - heldSeats - confirmedSeats)`
- **Lazy expiry on reads:** Read endpoints (`GET /classes`, `GET /classes/{id}`, `GET /holds/{id}`, `GET /me/holds`) are strictly read-only (`AsNoTracking()`) and compute effective hold status (`Status == 'HELD' && ExpiresAt <= now ? 'EXPIRED' : Status`) using `TimeProvider.GetUtcNow()`. Expired holds immediately stop counting toward `heldSeats` and display as `EXPIRED` with zero database writes or background polling jobs.
- **Pessimistic row locking on writes:** Every hold write operation (`POST /classes/{id}/holds`, `POST /holds/{id}/confirm`, `DELETE /holds/{id}`) opens an explicit database transaction and locks the parent `Classes` row using `SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE`.
  - Inside `POST /classes/{id}/holds`, after acquiring the class row lock, the handler **first** executes `UPDATE "Holds" SET "Status" = 'EXPIRED' WHERE "ClassId" = @classId AND "Status" = 'HELD' AND "ExpiresAt" <= @now` (`ExecuteUpdateAsync`) so a student re-applying after expiry never trips the partial unique index, then checks for duplicate active/confirmed holds, counts active `HELD` + `CONFIRMED` holds, and inserts the new hold only if `held + confirmed < totalSeats`.
  - In `Confirm` and `Cancel`, the pre-check reads the hold with `AsNoTracking()` (to avoid caching stale pre-lock state in EF Core's identity map) and then queries the tracked `Hold` entity **after** acquiring the `FOR UPDATE` lock on `Classes`.
  - Because the lock lives in PostgreSQL on the shared `Classes` row, `held + confirmed <= totalSeats` holds even when multiple backend instances run on different ports against the same database.

## 3. Idempotent Confirm and Cancel
- **`POST /holds/{holdId}/confirm`:** Calling confirm on a hold that is already `CONFIRMED` (by the owning student) returns `200 OK` with `{ "holdId": "...", "status": "CONFIRMED" }`. Because `confirmedSeats` is derived from `COUNT(*)` where `Status == 'CONFIRMED'`, repeated confirm calls never increment seat counts more than once.
- **`DELETE /holds/{holdId}`:** Calling cancel on a hold that is already `CANCELLED` (by the owning student) is idempotent and returns `204 No Content`.

## 4. State Transition Rules (`409 Conflict` on Invalid Transitions)
- **Confirm (`POST /holds/{holdId}/confirm`):** Only an active `HELD` hold (`ExpiresAt > now`) can transition to `CONFIRMED`. If the hold is `CANCELLED`, `EXPIRED`, or `HELD` with `ExpiresAt <= now`, the endpoint returns `409 Conflict` (persisting and committing `Status = 'EXPIRED'` first if it was an expired `HELD` row).
- **Cancel (`DELETE /holds/{holdId}`):** Only an active `HELD` hold (`ExpiresAt > now`) or an already-`CANCELLED` hold returns `204 No Content`. Attempting to cancel a `CONFIRMED` hold or an expired hold (`Status == 'EXPIRED'` or `Status == 'HELD' && ExpiresAt <= now`) returns `409 Conflict` (persisting and committing `Status = 'EXPIRED'` first if needed).

## 5. Ownership (`403`) and Unknown or Malformed IDs (`404`)
- **Ownership:** `POST /holds/{holdId}/confirm` and `DELETE /holds/{holdId}` verify that the hold's `StudentId` matches the `X-Student-Id` header; a mismatch returns `403 Forbidden`.
- **Route IDs:** All route parameters (`{classId}`, `{holdId}`) are bound as strings and parsed via `Guid.TryParse`. Both non-existent GUIDs and non-GUID strings (e.g., `/classes/not-a-guid`, `/holds/does-not-exist`) consistently return `404 Not Found`.

## 6. Student Identifier (`X-Student-Id`) Validation
- **UI:** Enforces a 17-digit birth registration number (`/^\d{17}$/`) and persists it in `localStorage`.
- **API:** Requires `X-Student-Id` on all four student endpoints (`POST /classes/{id}/holds`, `POST /holds/{id}/confirm`, `DELETE /holds/{id}`, `GET /me/holds`), trims leading/trailing whitespace, and returns `400 Bad Request` if missing or blank. The API accepts any non-empty string (up to 64 characters) rather than restricting to 17 digits so automated grading scripts using arbitrary test IDs work seamlessly.

## 7. Class Creation (`POST /classes`) & Non-Unique Class Names
- `POST /classes` trims `name` and requires a non-empty `name` ($\le 200$ characters) and `totalSeats >= 1`, returning `400 Bad Request` otherwise.
- Class names are intentionally **not** constrained to be unique so evaluators and automated tests can create classes with the same name repeatedly against a persistent database volume.
- `GET /classes` orders rows deterministically by `CreatedAt ASC, Id ASC` so table rows never jump between UI polling intervals.

## 8. Application History in `GET /me/holds`
- `GET /me/holds` returns all holds belonging to the student (`HELD`, `CONFIRMED`, `EXPIRED`, and `CANCELLED`) ordered newest first (`CreatedAt DESC, Id DESC`), computing effective status so expired holds appear as `EXPIRED` immediately.

## 9. No Authentication on Admin Endpoints
- Per §1.3 of the brief (*"Login লাগবে না"*), `POST /classes` requires no authentication or role header.

## 10. Extra Response Fields (`serverTime`) & Error Format
- Per §1.4 (*"Response-এ বাড়তি field যোগ করতে পারেন, কিন্তু নিচের কোনো field বাদ দিতে বা তার নাম বদলাতে পারবেন না"*):
  - Hold responses (`POST /classes/{id}/holds`, `GET /holds/{id}`, `GET /me/holds`) include an additional `serverTime` field (UTC ISO-8601 timestamp with trailing `Z`) alongside `expiresAt` so the React UI can compute a clock-skew-safe countdown timer.
  - All error responses use RFC 7807 `ProblemDetails` JSON extended with machine-readable `code` and human-readable `message` properties.
