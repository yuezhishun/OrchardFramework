#!/usr/bin/env bash
set -Eeuo pipefail

usage() {
  cat <<'EOF'
Usage:
  scripts/deploy_saas.sh [options]

Options:
  --skip-tests        Skip `dotnet test`.
  --skip-frontend     Skip frontend build/deploy.
  --skip-backend      Skip backend build/deploy.
  --health-timeout N  Health-check timeout in seconds. Default: 45.
  -h, --help          Show this help.

Environment overrides:
  SERVICE_NAME        systemd service name (default: orchardframework-saas.service)
  APP_ROOT            deployment root (default: /www/wwwroot/pty.addai.vip)
  API_DEPLOY_DIR      backend deploy dir (default: $APP_ROOT/saas-api)
  WEB_DEPLOY_DIR      frontend deploy dir (default: $APP_ROOT/saas)
  BACKUP_ROOT         backup root (default: /www/backup)
  HEALTH_URL          health check url (default: https://pty.addai.vip/saas/api/saas/summary)
  FRONTEND_BASE       frontend build base path (default: /saas/)
EOF
}

RUN_TESTS=1
DO_FRONTEND=1
DO_BACKEND=1
HEALTH_TIMEOUT=45

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-tests)
      RUN_TESTS=0
      shift
      ;;
    --skip-frontend)
      DO_FRONTEND=0
      shift
      ;;
    --skip-backend)
      DO_BACKEND=0
      shift
      ;;
    --health-timeout)
      HEALTH_TIMEOUT="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ "$DO_BACKEND" -eq 0 && "$DO_FRONTEND" -eq 0 ]]; then
  echo "Nothing to do: both backend and frontend are skipped." >&2
  exit 1
fi

if ! [[ "$HEALTH_TIMEOUT" =~ ^[0-9]+$ ]] || [[ "$HEALTH_TIMEOUT" -lt 1 ]]; then
  echo "--health-timeout must be a positive integer." >&2
  exit 1
fi

log() {
  printf '[%s] %s\n' "$(date '+%F %T')" "$*"
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

run_dotnet() {
  env -u version dotnet "$@"
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SERVICE_NAME="${SERVICE_NAME:-orchardframework-saas.service}"
APP_ROOT="${APP_ROOT:-/www/wwwroot/pty.addai.vip}"
API_DEPLOY_DIR="${API_DEPLOY_DIR:-${APP_ROOT}/saas-api}"
WEB_DEPLOY_DIR="${WEB_DEPLOY_DIR:-${APP_ROOT}/saas}"
BACKUP_ROOT="${BACKUP_ROOT:-/www/backup}"
HEALTH_URL="${HEALTH_URL:-https://pty.addai.vip/saas/api/saas/summary}"
FRONTEND_BASE="${FRONTEND_BASE:-/saas/}"
SOLUTION_PATH="${SOLUTION_PATH:-${REPO_ROOT}/OrchardFramework.slnx}"
API_PROJECT_PATH="${API_PROJECT_PATH:-${REPO_ROOT}/src/OrchardFramework.Api/OrchardFramework.Api.csproj}"

TIMESTAMP="$(date '+%Y%m%d%H%M%S')"
API_RELEASE_DIR="/tmp/orchardframework-saas-api-${TIMESTAMP}"
FRONTEND_RELEASE_DIR="/tmp/orchardframework-saas-frontend-${TIMESTAMP}"
API_BACKUP_DIR="${BACKUP_ROOT}/saas-api-${TIMESTAMP}"
WEB_BACKUP_DIR="${BACKUP_ROOT}/saas-frontend-${TIMESTAMP}"
LOCK_FILE="/tmp/orchardframework-saas-deploy.lock"
SERVICE_STOPPED=0
BACKUP_READY=0

cleanup() {
  rm -rf "${API_RELEASE_DIR}" "${FRONTEND_RELEASE_DIR}"
}
trap cleanup EXIT

rollback() {
  log "Rolling back deployment..."
  if [[ "$DO_BACKEND" -eq 1 ]]; then
    rsync -a --delete "${API_BACKUP_DIR}/" "${API_DEPLOY_DIR}/"
  fi
  if [[ "$DO_FRONTEND" -eq 1 ]]; then
    rsync -a --delete "${WEB_BACKUP_DIR}/" "${WEB_DEPLOY_DIR}/"
  fi
  systemctl start "${SERVICE_NAME}"
  SERVICE_STOPPED=0
}

handle_error() {
  local code="$?"
  trap - ERR
  if [[ "${SERVICE_STOPPED}" -eq 1 ]]; then
    if [[ "${BACKUP_READY}" -eq 1 ]]; then
      rollback || true
    else
      systemctl start "${SERVICE_NAME}" || true
      SERVICE_STOPPED=0
    fi
  fi
  log "Deployment failed with exit code ${code}."
  exit "${code}"
}
trap handle_error ERR

require_cmd flock
require_cmd rsync
require_cmd curl
require_cmd systemctl
require_cmd dotnet
if [[ "${DO_FRONTEND}" -eq 1 ]]; then
  require_cmd npm
fi

exec 9>"${LOCK_FILE}"
if ! flock -n 9; then
  echo "Another deployment is running. Lock file: ${LOCK_FILE}" >&2
  exit 1
fi

mkdir -p "${API_RELEASE_DIR}" "${FRONTEND_RELEASE_DIR}" "${BACKUP_ROOT}"

if [[ "${DO_BACKEND}" -eq 1 ]]; then
  log "Backend build: restore + build + test/publish."
  run_dotnet restore "${SOLUTION_PATH}"
  run_dotnet build "${SOLUTION_PATH}" -c Release --nologo
  if [[ "${RUN_TESTS}" -eq 1 ]]; then
    run_dotnet test "${SOLUTION_PATH}" -c Release --no-build --nologo
  fi
  run_dotnet publish "${API_PROJECT_PATH}" -c Release -o "${API_RELEASE_DIR}" --nologo
fi

if [[ "${DO_FRONTEND}" -eq 1 ]]; then
  log "Frontend build: npm ci + vite build."
  npm --prefix "${REPO_ROOT}/frontend" ci
  npm --prefix "${REPO_ROOT}/frontend" run build -- --base="${FRONTEND_BASE}"
  rsync -a --delete "${REPO_ROOT}/frontend/dist/" "${FRONTEND_RELEASE_DIR}/"
fi

if [[ "${DO_BACKEND}" -eq 1 && ! -f "${API_RELEASE_DIR}/OrchardFramework.Api.dll" ]]; then
  echo "Backend publish output is invalid: OrchardFramework.Api.dll not found." >&2
  exit 1
fi
if [[ "${DO_FRONTEND}" -eq 1 && ! -f "${FRONTEND_RELEASE_DIR}/index.html" ]]; then
  echo "Frontend build output is invalid: index.html not found." >&2
  exit 1
fi

log "Stopping service ${SERVICE_NAME}."
systemctl stop "${SERVICE_NAME}"
SERVICE_STOPPED=1

log "Creating backup snapshots."
if [[ "${DO_BACKEND}" -eq 1 ]]; then
  mkdir -p "${API_BACKUP_DIR}"
  rsync -a "${API_DEPLOY_DIR}/" "${API_BACKUP_DIR}/"
fi
if [[ "${DO_FRONTEND}" -eq 1 ]]; then
  mkdir -p "${WEB_BACKUP_DIR}"
  rsync -a "${WEB_DEPLOY_DIR}/" "${WEB_BACKUP_DIR}/"
fi
BACKUP_READY=1

if [[ "${DO_BACKEND}" -eq 1 ]]; then
  log "Deploying backend files."
  rsync -a --delete --exclude data/ --exclude App_Data/ "${API_RELEASE_DIR}/" "${API_DEPLOY_DIR}/"
fi
if [[ "${DO_FRONTEND}" -eq 1 ]]; then
  log "Deploying frontend files."
  rsync -a --delete "${FRONTEND_RELEASE_DIR}/" "${WEB_DEPLOY_DIR}/"
fi

log "Starting service ${SERVICE_NAME}."
systemctl start "${SERVICE_NAME}"
SERVICE_STOPPED=0

log "Waiting for health check: ${HEALTH_URL}"
deadline=$((SECONDS + HEALTH_TIMEOUT))
while (( SECONDS < deadline )); do
  code="$(curl -s -o /tmp/orchardframework-saas-health-${TIMESTAMP}.json -w '%{http_code}' "${HEALTH_URL}" || true)"
  if [[ "${code}" == "200" ]]; then
    log "Deployment succeeded."
    log "Backups kept at: ${API_BACKUP_DIR} and ${WEB_BACKUP_DIR}"
    exit 0
  fi
  sleep 2
done

log "Health check timeout. Triggering rollback."
rollback
exit 1
