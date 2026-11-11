# School Admission System

Online, first-come-first-served school admission. A parent applies for a seat in a class (one shift = one class), which places a time-limited **hold**. Paying the admission fee (simulated) **confirms** it; an unpaid hold expires and frees the seat.

**Stack:** ASP.NET Core (.NET 10) · PostgreSQL 16 · React 19 + TypeScript (Vite) · Docker Compose

## Run with Docker

Only Docker (with Compose) is required.

```bash
docker compose up
```

The first run builds the images and starts PostgreSQL, the backend and the frontend. Use `docker compose up --build` to rebuild after changing code.

| | URL |
| :--- | :--- |
| **Frontend (open this)** | http://localhost:3000 |
| Backend API | http://localhost:8080 |

To run an optional second backend instance on port 8081 sharing the same database (for multi-process scaling), use `docker compose --profile scale up`.

Everything is done from the UI: enter the student's 17-digit birth registration number at the top, create classes in **Class তৈরি**, apply from **Class তালিকা**, then pay or cancel in **আবেদন**. **আমার আবেদন** lists the student's applications.

Data is kept in the `pgdata` Docker volume, so `docker compose down` followed by `docker compose up` keeps it. (`docker compose down -v` deletes it.)

## Run the tests

```bash
docker compose run --rm tests
```

These are backend xUnit integration tests running against a real PostgreSQL (a separate `admission_system_test` database; the database container is started automatically). They include concurrency tests: many parallel applications for one class never exceed `totalSeats`, on one backend instance and on two instances sharing one database.

## Configuration

All values can be overridden from the shell when starting the stack.

| Variable | Default | Meaning |
| :--- | :--- | :--- |
| `HOLD_DURATION_SECONDS` | `600` | How long a hold lasts. Invalid or non-positive values fall back to 600. |
| `ConnectionStrings__DefaultConnection` | *(unset)* | Npgsql connection string for the backend. Takes precedence when set. |
| `DATABASE_CONNECTION_STRING` | bundled `db` container (local dev credentials)| Npgsql connection string, used when the variable above is unset. |
| `API_URL` | `http://localhost:8080` | Backend URL the browser calls. Applied when the frontend container starts, so no rebuild is needed. |

Example, a 30-second hold to watch expiry in the UI:

```bash
HOLD_DURATION_SECONDS=30 docker compose up
```

## More

- [ASSUMPTIONS.md](./ASSUMPTIONS.md): decisions where the brief is silent, and how concurrency is handled
- [AI_USAGE.md](./AI_USAGE.md): which AI tools were used, where they were wrong, and how that was caught
