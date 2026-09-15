# PLS — Patient Location System

Emergency triage / patient tracking system: .NET 8 API backend (JWT auth, MQTT
publishing) with a React 19 frontend, backed by MySQL.

## Running

Copy `env.example` to `.env` and fill in all values (see comments in that
file for what's required).

Before first run, generate the mosquitto password file (not committed to git):

```sh
docker run --rm -v "$(pwd)/mosquitto:/mosquitto" eclipse-mosquitto:2 \
  mosquitto_passwd -b -c /mosquitto/passwd pls-backend "$MQTT_PASSWORD"
```

Use the same password here as `MQTT_PASSWORD` in `.env`.

**Production:**

```sh
docker compose up --build
```

**Development** (adds dev-only sample data seeding, hot reload, etc.):

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

The app is reachable at `http://localhost:8080` once `pls-app` is healthy.

Always rebuild with the `up --build` form above (not `docker compose build`
followed by `up --no-deps`) — `--no-deps` starts only the given service and
leaves `mysql_db`/`mosquitto` down, which the API needs to connect to.

## Project layout

- `src/PLS.Api/` — .NET 8 backend
- `client-app/` — React frontend
- `tests/PLS.Api.Tests/` — backend integration tests
