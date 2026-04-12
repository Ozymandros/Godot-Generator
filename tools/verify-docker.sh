#!/usr/bin/env bash
# Local/manual: runs tools/verify.ps1 (full suite, including E2E) inside the Docker image used by .devcontainer.
# Usage: ./tools/verify-docker.sh   |   NO_BUILD=1 ./tools/verify-docker.sh
set -euo pipefail
cd "$(dirname "$0")/.."
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.verify.yml}"
if [[ ! -f "$COMPOSE_FILE" ]]; then
  echo "Missing $COMPOSE_FILE" >&2
  exit 1
fi
BUILD_ARGS=()
if [[ "${NO_BUILD:-}" != "1" ]]; then
  BUILD_ARGS+=(--build)
fi
exec docker compose -f "$COMPOSE_FILE" run "${BUILD_ARGS[@]}" --rm verify
