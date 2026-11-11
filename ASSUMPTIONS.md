# Assumptions

Where the brief does not say, this is what I assumed and why.

## Applications (holds)

- **One active application per student per class.** A student cannot hold or confirm two seats in the same class, but can apply to different classes. Morning and Day shift are separate classes, and parents often apply to both. This is enforced inside the locked transaction (`409 DUPLICATE_HOLD`) and, as a safety net, by a partial unique index on `(ClassId, StudentId)` for `HELD`/`CONFIRMED` rows.
- **Expiry is lazy.** Each hold stores its own `expiresAt`; there is no background job. Anything that reads a hold treats `HELD` with `expiresAt <= now` as `EXPIRED` and stops counting it, so the seat frees at the exact expiry instant with no API call, and it still works after a restart. Writes that touch such a hold also persist `EXPIRED`.
- **Hold length** comes from `HOLD_DURATION_SECONDS` when the application is made and is stored per hold. Changing it only affects new holds.
- **Confirm** is idempotent: confirming an already `CONFIRMED` hold returns `200` and changes nothing. Confirming a `CANCELLED` or expired hold returns `409`.
- **Cancel** works only on a `HELD` hold. Cancelling an already `CANCELLED` hold returns `204`. A `CONFIRMED` hold cannot be cancelled and an expired one cannot be cancelled (`409`); the brief defines no refund flow, so a confirmed admission is final.
- **Payment is simulated.** "ভর্তি ফি দিন" is treated as a successful payment, as the brief says.

## Status codes

- Unknown or malformed ids (for example `/holds/not-a-guid`) return `404`.
- Confirming or cancelling another student's hold returns `403`.
- A missing, blank or over-long `X-Student-Id` returns `400`.
- Duplicate application and no free seat both return `409`; the `code` field (`DUPLICATE_HOLD`, `NO_SEATS`) tells them apart.
- Every error body is JSON with `code` and `message` (RFC 7807 problem details plus those two fields).

## Student identity

- The birth registration number is sent as `X-Student-Id`. The UI requires exactly 17 digits (Bengali digits are converted to 0-9) and remembers it in the browser.
- The API accepts any non-blank value up to 64 characters, so automated clients can use any test id.

## Classes

- Class names are not unique, so the same name can be created repeatedly. The UI adds a short `#id` tag when two classes share a name.
- `POST /classes` trims the name and requires 1–200 characters and `totalSeats >= 1`. The UI also caps seats at 100,000 as a client-side safeguard; the API does not.
- There is no authentication for class creation, as the brief says no login is needed. There is no edit or delete for classes.
- `GET /classes` lists classes in creation order; `GET /me/holds` lists the student's applications newest first, including expired and cancelled ones.

## Response format

- Every field in the brief is present with the exact name. The only extra field is `serverTime` on hold responses; the UI uses it to compute a countdown that does not depend on the browser's clock.
- Timestamps are UTC ISO-8601 with a trailing `Z` and millisecond precision, identical across endpoints for the same hold.

## Concurrency

Every request that changes seats (apply, confirm, cancel) runs in one transaction that first locks the class row (`SELECT … FOR UPDATE`). PostgreSQL therefore processes changes for one class one at a time, while different classes do not block each other. Seat counts are never stored: they are computed from the holds table inside that lock, so `held + confirmed <= totalSeats` holds however many requests arrive, and confirm stays idempotent because it only checks stored state.

**Two backend processes on different ports sharing one database** are safe by design: the lock is a PostgreSQL row lock rather than an in-process one, and the backend keeps no seat state in memory, so a second process is subject to the same lock. `MultiInstanceConcurrencyTests` starts two API hosts against one database and sends 100 apply requests to each for a 20-seat class: exactly 20 succeed and 180 get `409`. That test runs two hosts inside one test process, not two containers, but the locking is done by PostgreSQL either way. Each process uses its own injectable clock (`TimeProvider`) rather than SQL `NOW()` so automated tests can deterministically advance time without sleeping, which is fine on one machine; on separate machines, clock skew would shift expiry by the skew.

## UI

- One page with the four sections from the brief and no login. The selected application, and the student number, are restored after a refresh or browser restart.
- Seat counts and applications refresh every 3 seconds without reloading.
- API error codes are mapped to plain Bengali/English messages, including "server unreachable" when the backend cannot be reached.
