#!/usr/bin/env bash
set -euo pipefail

# ---------------------------------------------------------------------------
# restore.sh — Restore a PLS database dump into the running mysql_db container.
#
# Usage:
#   ./scripts/restore.sh <DUMP_FILE>
#   ./scripts/restore.sh -h
#
# The script will:
#   1. Validate that the dump file exists and is readable.
#   2. Prompt for confirmation before making any changes.
#   3. Stop pls-app so no writes occur during restore.
#   4. Drop and recreate the 'pls' database.
#   5. Pipe the dump (decompressing .gz files automatically) into mysql.
#   6. Restart pls-app.
#
# Credentials are read from the .env file in the project root.
# ---------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

MYSQL_SERVICE="mysql_db"
APP_SERVICE="pls-app"
DB_NAME="pls"
ENV_FILE="${PROJECT_ROOT}/.env"

usage() {
  cat <<EOF
Usage: $(basename "$0") [-h] <DUMP_FILE>

Restore a PLS database from a mysqldump file into the running ${MYSQL_SERVICE} container.

Arguments:
  DUMP_FILE   Path to the dump file to restore. Accepts plain .sql or
              gzip-compressed .sql.gz files.

Options:
  -h          Show this help message and exit.

WARNING: This will DROP and recreate the '${DB_NAME}' database.
         All current data will be permanently lost.

The script reads DB_USER and DB_PASSWORD from the .env file at:
  ${ENV_FILE}

Example:
  $(basename "$0") ./backups/pls_backup_20260319_020000.sql.gz
EOF
}

# Parse flags
while getopts ":h" opt; do
  case "${opt}" in
    h) usage; exit 0 ;;
    \?) echo "Unknown option: -${OPTARG}" >&2; usage >&2; exit 1 ;;
  esac
done
shift $((OPTIND - 1))

if [[ $# -lt 1 ]]; then
  echo "ERROR: A dump file argument is required." >&2
  usage >&2
  exit 1
fi

DUMP_FILE="${1}"

# ---------------------------------------------------------------------------
# Validate dump file
# ---------------------------------------------------------------------------
if [[ ! -f "${DUMP_FILE}" ]]; then
  echo "ERROR: Dump file not found: ${DUMP_FILE}" >&2
  exit 1
fi

if [[ ! -r "${DUMP_FILE}" ]]; then
  echo "ERROR: Dump file is not readable: ${DUMP_FILE}" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Load credentials from .env
# ---------------------------------------------------------------------------
if [[ ! -f "${ENV_FILE}" ]]; then
  echo "ERROR: .env file not found at ${ENV_FILE}" >&2
  echo "       Copy .env.example to .env and fill in the required values." >&2
  exit 1
fi

DB_USER=""
DB_PASSWORD=""
while IFS='=' read -r key value; do
  value="${value%%#*}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  case "${key}" in
    DB_USER)     DB_USER="${value}" ;;
    DB_PASSWORD) DB_PASSWORD="${value}" ;;
  esac
done < <(grep -E '^[[:space:]]*(DB_USER|DB_PASSWORD)[[:space:]]*=' "${ENV_FILE}")

if [[ -z "${DB_USER}" ]]; then
  echo "ERROR: DB_USER is not set in ${ENV_FILE}" >&2
  exit 1
fi
if [[ -z "${DB_PASSWORD}" ]]; then
  echo "ERROR: DB_PASSWORD is not set in ${ENV_FILE}" >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Confirmation prompt
# ---------------------------------------------------------------------------
DUMP_ABS="$(realpath "${DUMP_FILE}")"
echo "=========================================================="
echo "  PLS DATABASE RESTORE"
echo "=========================================================="
echo "  Dump file : ${DUMP_ABS}"
echo "  Database  : ${DB_NAME} (inside ${MYSQL_SERVICE})"
echo ""
echo "  WARNING: The current '${DB_NAME}' database will be"
echo "           DROPPED and replaced with the contents of the"
echo "           dump file. This action cannot be undone."
echo "=========================================================="
read -r -p "Type 'yes' to confirm and proceed: " CONFIRM

if [[ "${CONFIRM}" != "yes" ]]; then
  echo "Restore cancelled. No changes were made."
  exit 0
fi

# ---------------------------------------------------------------------------
# Stop pls-app
# ---------------------------------------------------------------------------
cd "${PROJECT_ROOT}"

echo ""
echo "[1/4] Stopping ${APP_SERVICE}..."
docker compose stop "${APP_SERVICE}"
echo "      ${APP_SERVICE} stopped."

# ---------------------------------------------------------------------------
# Drop and recreate the database
# ---------------------------------------------------------------------------
echo "[2/4] Dropping and recreating database '${DB_NAME}'..."
docker compose exec -T "${MYSQL_SERVICE}" \
  mysql -u"${DB_USER}" -p"${DB_PASSWORD}" \
  -e "DROP DATABASE IF EXISTS \`${DB_NAME}\`; CREATE DATABASE \`${DB_NAME}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
echo "      Database recreated."

# ---------------------------------------------------------------------------
# Restore the dump
# ---------------------------------------------------------------------------
echo "[3/4] Restoring dump into '${DB_NAME}'..."

if [[ "${DUMP_FILE}" == *.gz ]]; then
  # Decompress on the fly
  gzip -dc "${DUMP_ABS}" \
    | docker compose exec -T "${MYSQL_SERVICE}" \
        mysql -u"${DB_USER}" -p"${DB_PASSWORD}" "${DB_NAME}"
else
  docker compose exec -T "${MYSQL_SERVICE}" \
    mysql -u"${DB_USER}" -p"${DB_PASSWORD}" "${DB_NAME}" \
    < "${DUMP_ABS}"
fi

echo "      Restore complete."

# ---------------------------------------------------------------------------
# Restart pls-app
# ---------------------------------------------------------------------------
echo "[4/4] Starting ${APP_SERVICE}..."
docker compose up -d "${APP_SERVICE}"
echo "      ${APP_SERVICE} started."

echo ""
echo "SUCCESS: Database restored from ${DUMP_ABS}"
echo ""
echo "Next steps:"
echo "  - Check health : curl -sf http://localhost:8080/health"
echo "  - Check logs   : docker compose logs ${APP_SERVICE} --since 2m"
