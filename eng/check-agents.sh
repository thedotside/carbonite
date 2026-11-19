#!/usr/bin/env bash
# Checks what the repository gives to coding agents, without building anything:
#   - the plugin version is the package version of Directory.Build.props;
#   - no plugin component is tracked at the repository root, which is the root of the carbonite
#     plugin: it would ship code or configuration to every user of the plugin;
#   - every code block of the skill has a known tag (eng/skill-examples.sh).
# CI then runs the official validators: gh skill publish --dry-run . and
# claude plugin validate --strict .
#
# Usage: eng/check-agents.sh
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo"
problems=0
problem() { printf '%s\n' "$1" >&2; problems=$((problems + 1)); }

package="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
plugin="$(sed -n 's:.*"version"[[:space:]]*\:[[:space:]]*"\([^"]*\)".*:\1:p' .claude-plugin/plugin.json | head -n 1)"
if [[ "$plugin" != "$package" ]]; then
  problem ".claude-plugin/plugin.json: version is '$plugin' but Directory.Build.props says '$package': change both together."
fi

# What Claude Code and Copilot CLI load from the root of a plugin, besides skills/.
for path in .mcp.json mcp.json .github/mcp.json .lsp.json lsp.json .github/lsp.json hooks.json hooks \
  agents commands output-styles workflows themes monitors bin com.github.copilot; do
  if [[ -n "$(git ls-files -- "$path")" ]]; then
    problem "$path: the repository root is the root of the carbonite plugin, so this would reach every user of the plugin. Keep plugin components out of the root."
  fi
done

examples="$(mktemp -d)"
trap 'rm -rf "$examples"' EXIT
bash eng/skill-examples.sh "$examples" > /dev/null || problem "skills/carbonite: fix the code blocks reported above."

if [[ $problems -gt 0 ]]; then
  echo "Agent files check failed: $problems problem(s)." >&2
  exit 1
fi
echo "Agent files check passed (plugin $plugin)."
