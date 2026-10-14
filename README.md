# PLS — Patient Location System

Emergency triage / patient tracking system: .NET 8 API backend (JWT auth, MQTT
publishing) with a React 19 frontend, backed by MySQL.

## Running

### Production (Debian/Ubuntu VM or LXC)

On the server, from a checkout of this repository:

```sh
sudo bash install.sh <public-dns-name> <reverse-proxy-ip>
# e.g. sudo bash install.sh pls.example.org 10.11.7.100
```

On the first run `install.sh` creates `.env` with random secrets (only once, never overwritten),
generates `mosquitto/passwd`, builds and starts the stack, installs a nightly backup timer and
prints the initial `admin` password. An LXC container needs `nesting=1,keyctl=1` under
Options → Features.

`.env` holds all secrets. Keep a copy outside the server; never delete or regenerate it for an
existing database (MySQL keeps its original passwords).

The compose project is always named `pls`, so its volumes (`pls_mysql_data`) don't depend on the
folder name. A stack started before that keeps its data in `<folder>_mysql_data`; move it over
(dump and restore) before running `install.sh` there.

**Behind a reverse proxy (e.g. NPMplus):** forward `http://<this server>:8080` and set a
certificate with Force SSL there. `TRUSTED_PROXY` in `.env` is the proxy's IP: only it may pass
on the real client IP, which the login rate limit (10 per minute per IP, `LOGIN_RATE_LIMIT` in
`.env`) depends on. After changing `.env`, re-run `sudo bash install.sh`.

**Upgrade:** `git pull`, then `sudo bash install.sh` again. It dumps the database first;
migrations run forward only, so that dump is the rollback point (`scripts/restore.sh`).

**Backups:** `pls-backup.timer` runs `scripts/backup.sh` at 01:30 and keeps the newest 14 dumps
in `backups/` (`BACKUP_KEEP` in `.env`, then re-run `install.sh`). Check with
`systemctl list-timers pls-backup.timer`. Dumps inside the container die with it: back up the
container (including `.env` and `backups/`) off-host as well.

### Development

Copy `.env.example` to `.env` and fill in all values (see comments in that
file for what's required).

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

This adds dev-only sample data seeding, anonymous MQTT, Swagger, etc.
The app is reachable at `http://localhost:8080` once `pls-app` is healthy.

Always rebuild with the `up --build` form above (not `docker compose build`
followed by `up --no-deps`) — `--no-deps` starts only the given service and
leaves `mysql_db`/`mosquitto` down, which the API needs to connect to.

## Project layout

- `src/PLS.Api/` — .NET 8 backend
- `client-app/` — React frontend
- `tests/PLS.Api.Tests/` — backend integration tests
