# AI Usage

## Tools and what I used them for

| Tool | Used for |
| :--- | :--- |
| **Claude (chat)** | Breaking the brief into phases, stress-testing the concurrency and lazy-expiry design, and reviewing API edge cases and documentation. |
| **Antigravity (IDE agent)** | Generating the implementation from my phase specs: ASP.NET Core endpoints, EF Core migrations, xUnit tests, Docker config, and the React UI. |

I drove the architecture and state-machine decisions (documented in `ASSUMPTIONS.md`), broke the work into small phases, reviewed every diff before committing, and verified behavior with `docker compose up`, `curl`, and the integration test suite.

## Where the AI was wrong, and how it was caught

1. **Seat limit with a single conditional `INSERT`.** The first design counted holds and inserted in one statement without a lock. Under PostgreSQL's default `READ COMMITTED`, two concurrent requests can both see one free seat and both insert. I caught it by reasoning about snapshots during design review. It was replaced with a transaction that locks the class row (`SELECT … FOR UPDATE`), and the 200-parallel-requests test checks the result.
2. **Lazy expiry vs. the unique index.** An expired hold is still stored as `HELD`, so a student re-applying after expiry would hit the partial unique index (`23505`). Found while designing the state machine. Expired rows are now flipped to `EXPIRED` first, inside the lock, and a test re-applies after expiry.
3. **Stale entity after taking the lock.** A tracked read before the lock lets EF Core return the old cached entity afterwards. Fixed with an untracked pre-check and a tracked read only after the lock.
4. **Timestamp format.** The AI assumed `DateTimeOffset` serializes with `Z`; it serializes as `+00:00`, but the brief's examples use `Z`. Caught by asserting on the raw JSON and checking `curl` output. Fixed by using UTC `DateTime` fields in the response DTOs.
5. **Timestamp precision (found by review, not by the tests).** `POST` returned 7 fractional digits (.NET ticks) while `GET` returned 6 (PostgreSQL), so `expiresAt` differed between endpoints for the same hold. The test clock started on a whole second, so the tests never saw it. Fixed by truncating to milliseconds, with a new test that uses a clock with sub-millisecond ticks.
6. **CORS ordering.** `UseCors` was registered after the exception handler, which could drop CORS headers from error responses and make the browser report a network error. Found in code review; `UseCors` now runs first and a test covers it.
7. **Compose hard-coded the connection string**, so the "changeable by environment variable" requirement was not met. Found by comparing the README with the compose file. It now passes the variables through with defaults.
8. **Docker and tooling versions.** The Dockerfile used `.sln` where .NET 10 generates `.slnx`, and `sdk:8.0` images for a `net10.0` project; a `/swagger` readiness check does not exist in .NET 9/10 templates (replaced by `/health`); `node:20` was too close to Vite's minimum (now `node:22`); the Postgres healthcheck used the wrong user (`postgres` instead of `admin`). All surfaced as build or log errors during `docker compose up --build`.
9. **UI.** The first plan had three screens instead of the four in the brief, and the API URL was baked in at build time, so `API_URL` at start-up was ignored. Caught by re-reading the brief and testing an override. The URL is now injected into `config.js` when the frontend container starts.
10. **Test clock.** `FakeTimeProvider` cannot move backwards, so resetting it between tests threw. Each test now gets a fresh instance.
