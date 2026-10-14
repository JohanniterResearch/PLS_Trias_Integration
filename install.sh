#!/usr/bin/env bash
# Installs or upgrades PLS on a Debian/Ubuntu VM or LXC from this checkout. Safe to re-run.
#   sudo bash install.sh [SITE_ADDRESS] [TRUSTED_PROXY]
#   SITE_ADDRESS   public DNS name clients use (default: first IP of this machine)
#   TRUSTED_PROXY  IP of the reverse proxy in front of this machine (e.g. NPMplus); empty = none
# Both are only read on the very first run, when .env is created.
set -euo pipefail
cd "$(dirname "$(readlink -f "$0")")"

[ "$(id -u)" -eq 0 ] || { echo "Please run: sudo bash install.sh" >&2; exit 1; }

if ! command -v docker >/dev/null || ! docker compose version >/dev/null 2>&1; then
  echo "==> Installing Docker (needs internet)"
  apt-get update -q
  apt-get install -y -q curl ca-certificates
  curl -fsSL https://get.docker.com | sh
fi
systemctl enable --now docker >/dev/null

# .env is generated exactly once. MySQL only applies the DB passwords on first init, so
# regenerating them would lock the app out of its own database.
if [ ! -f .env ]; then
  site=${1:-$(hostname -I | awk '{print $1}')}
  # The app refuses to start on anything but IP addresses here; fail now, not after the build.
  [[ "${2:-}" =~ ^[0-9A-Fa-f:.,]*$ ]] || { echo "TRUSTED_PROXY must be IP addresses, got '$2'." >&2; exit 1; }
  created=1
  (umask 077; cat > .env <<EOF
DB_USER=pls_user
DB_PASSWORD=$(openssl rand -hex 24)
DB_ROOT_PASSWORD=$(openssl rand -hex 24)
JWT_SECRET=$(openssl rand -hex 32)
ADMIN_USERNAME=admin
ADMIN_PASSWORD=$(openssl rand -hex 8)
MQTT_USERNAME=pls-backend
MQTT_PASSWORD=$(openssl rand -hex 24)
MQTT_REDACT_NAME=true
MQTT_REDACT_LOCATION=false
ALLOWED_ORIGINS=https://$site
TRUSTED_PROXY=${2:-}
EOF
  )
  echo "==> Created .env for https://$site"
elif [ $# -gt 0 ]; then
  echo "==> .env already exists; ignoring arguments. Edit .env to change ALLOWED_ORIGINS/TRUSTED_PROXY." >&2
fi
set -a; . ./.env; set +a

# The broker's password file (gitignored). A missing file would be bind-mounted as an empty
# directory and the broker would never become healthy. The password goes through a 0600 file,
# not the command line (visible in ps), and is hashed in place.
[ -d mosquitto/passwd ] && rmdir mosquitto/passwd
rm -f mosquitto/passwd
(umask 077; printf '%s:%s\n' "$MQTT_USERNAME" "$MQTT_PASSWORD" > mosquitto/passwd)
docker run --rm -v "$PWD/mosquitto:/work" eclipse-mosquitto:2 \
  sh -c 'umask 077 && mosquitto_passwd -U /work/passwd' || { rm -f mosquitto/passwd; exit 1; }
# Read-only mount: the broker (uid 1883) cannot fix ownership itself.
chown 1883:1883 mosquitto/passwd && chmod 600 mosquitto/passwd

# Migrations are forward-only: an upgrade's rollback point is the dump taken right before it.
# An existing database is backed up even if the stack is stopped.
if docker volume inspect "${COMPOSE_PROJECT_NAME:-pls}_mysql_data" >/dev/null 2>&1; then
  echo "==> Backing up before upgrade"
  docker compose up -d --wait mysql_db
  bash scripts/backup.sh
fi

echo "==> Building and starting (the first build takes several minutes)"
docker compose up -d --build --remove-orphans

echo "==> Installing nightly backup timer"
cat > /etc/systemd/system/pls-backup.service <<EOF
[Unit]
Description=PLS database backup
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
WorkingDirectory=$PWD
Environment=BACKUP_KEEP=${BACKUP_KEEP:-14}
ExecStart=/usr/bin/env bash $PWD/scripts/backup.sh
EOF
cat > /etc/systemd/system/pls-backup.timer <<EOF
[Unit]
Description=Nightly PLS database backup

[Timer]
OnCalendar=*-*-* 01:30:00
Persistent=true

[Install]
WantedBy=timers.target
EOF
systemctl daemon-reload
systemctl enable --now pls-backup.timer >/dev/null

echo -n "==> Waiting for the app"
for _ in $(seq 60); do
  if curl -fs http://127.0.0.1:8080/health >/dev/null 2>&1; then
    ok=1; break
  fi
  echo -n .; sleep 3
done
echo
[ "${ok:-}" ] || { echo "App did not become healthy. Check: docker compose logs pls-app db-migrate" >&2; exit 1; }

echo
echo "PLS is running on port 8080 (allowed origin: $ALLOWED_ORIGINS)"
# Only on the run that created it: on upgrades it would be a stale credential in the scrollback.
if [ "${created:-}" ]; then
  echo "Initial admin login:  $ADMIN_USERNAME / $ADMIN_PASSWORD"
  echo "(you are asked to change it on first login)"
fi
