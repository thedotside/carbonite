#!/usr/bin/env bash
# Once per checkout: restores the pinned local tools (CSharpier) and enables the versioned
# .githooks directory, unless another hook setup already exists.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

dotnet tool restore

hooks_path="$(git config --local --get core.hooksPath || true)"
if [[ -n "$hooks_path" && "$hooks_path" != .githooks ]]; then
  echo "core.hooksPath is already set to '$hooks_path': integrate .githooks/pre-commit with it first." >&2
  exit 1
fi
default_hook="$(git rev-parse --git-path hooks/pre-commit)"
if [[ -z "$hooks_path" && -e "$default_hook" ]]; then
  echo "A pre-commit hook already exists in $default_hook: integrate it before enabling .githooks." >&2
  exit 1
fi
git config --local core.hooksPath .githooks
echo "Local tools restored; .githooks/pre-commit enabled for this checkout."
