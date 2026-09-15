#!/usr/bin/env bash
set -euo pipefail

# ---------------------------------------------------------------------------
# backup.sh — Create a timestamped, gzipped mysqldump of the PLS database.
#
# Usage:
#   ./scripts/backup.sh [OUTPUT_DIR]
#   ./scripts/backup.sh -h
#
# Arguments:
#   OUTPUT_DIR   Directory where the dump file is written.
#                Defaults to ./backups/ (relative to the project root).
#
# Credentials are read from the .env file in the project root.
# The MySQL service name matches docker-compose.yml: mysql_db
# ---------------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

MYSQL_SERVICE="mysql_db"
DB_NAME="pls"
ENV_FILE="${PROJECT_ROOT}/.env"

usage() {
  cat <<EOF
Usage: $(basename "$0") [-h] [OUTPUT_DIR]

Create a timestamped, gzipped mysqldump of the PLS database.

Arguments:
  OUTPUT_DIR   Directory to write the backup file into.
               Defaults to: ${PROJECT_ROOT}/backups/

Options:
  -h           Show this help message and exit.

The script reads DB_USER and DB_PASSWORD from the .env file at:
  ${ENV_FILE}

Example:
  $(basename "$0")                          # writes to ./backups/
  $(basename "$0") /var/backups/pls/        # writes to /var/backups/pls/
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

OUTPUT_DIR="${1:-${PROJECT_ROOT}/backups}"

# ---------------------------------------------------------------------------
# Load credentials from .env
# ---------------------------------------------------------------------------
if [[ ! -f "${ENV_FILE}" ]]; then
  echo "ERROR: .env file not found at ${ENV_FILE}" >&2
  echo "       Copy .env.example to .env and fill in the required values." >&2
  exit 1
fi

# Source only the DB_USER and DB_PASSWORD lines to avoid polluting the shell
# environment with unrelated variables.
DB_USER=""
DB_PASSWORD=""
while IFS='=' read -r key value; do
  # Strip inline comments and leading/trailing whitespace from value
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
# Prepare output directory
# ---------------------------------------------------------------------------
mkdir -p "${OUTPUT_DIR}"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
DUMP_FILE="${OUTPUT_DIR}/pls_backup_${TIMESTAMP}.sql.gz"

# ---------------------------------------------------------------------------
# Run the dump
# ---------------------------------------------------------------------------
echo "Starting backup of database '${DB_NAME}' from service '${MYSQL_SERVICE}'..."
echo "  Output: ${DUMP_FILE}"

cd "${PROJECT_ROOT}"

if docker compose exec -T "${MYSQL_SERVICE}" \
    mysqldump \
      --single-transaction \
      --routines \
      --triggers \
      -u"${DB_USER}" \
      -p"${DB_PASSWORD}" \
      "${DB_NAME}" \
    | gzip > "${DUMP_FILE}"; then
  DUMP_SIZE="$(du -sh "${DUMP_FILE}" | cut -f1)"
  echo "SUCCESS: Backup written to ${DUMP_FILE} (${DUMP_SIZE})"
else
  echo "FAILURE: mysqldump exited with an error. Removing incomplete file." >&2
  rm -f "${DUMP_FILE}"
  exit 1
fi
