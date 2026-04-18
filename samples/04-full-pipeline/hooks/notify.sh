#!/usr/bin/env bash
set -euo pipefail

# Read HookContext JSON from stdin
json=$(cat)
phase="${WEFT_HOOK_PHASE:-unknown}"
profile="${WEFT_HOOK_PROFILE:-unknown}"
db="${WEFT_HOOK_DATABASE:-unknown}"

echo "[${phase}] profile=${profile} db=${db}"
echo "  payload: $(echo "$json" | head -c 200)..."

# Stand-in for Teams/Slack POST:
# curl -X POST -H 'Content-Type: application/json' \
#   -d "{\"text\":\"[${phase}] ${profile}/${db}\"}" "$TEAMS_WEBHOOK"
