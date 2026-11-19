#!/usr/bin/env bash
# Writes every ```csharp block of the agent skill to <output-dir>/SkillExampleNN.cs, so that
# eng/package-smoke.sh compiles and runs what agents copy. Each block is a complete file; shorter
# C# in the skill is inline code. A code block in another language must use a known tag, so that
# a mistyped tag (```cs, ```C#, ```csharp title=…) fails here instead of escaping the check.
#
# Usage: eng/skill-examples.sh <output-dir> [skill-dir]   (default: skills/carbonite)
# Prints the number of examples. Fails on an unknown tag, an unclosed block, or no example at all.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${1:?usage: eng/skill-examples.sh <output-dir> [skill-dir]}"
skill="${2:-$repo/skills/carbonite}"
mkdir -p "$output"

# Every Markdown file of the skill, in a stable order (a while loop: bash 3.2 has no mapfile).
files=()
while IFS= read -r file; do files+=("$file"); done < <(find "$skill" -name '*.md' -type f | LC_ALL=C sort)
[[ ${#files[@]} -gt 0 ]] || { echo "No Markdown file in $skill." >&2; exit 1; }

count="$(awk -v dir="$output" '
  function problem(message) { printf "%s:%d: %s\n", FILENAME, FNR, message > "/dev/stderr"; failed = 1 }
  FNR == 1 {
    if (fence != "") printf "%s: a code block is not closed\n", previous > "/dev/stderr"
    if (fence != "") failed = 1
    fence = ""; inside = 0; previous = FILENAME
  }
  { line = $0; sub(/\r$/, "", line) }
  fence == "" && match(line, /^[ \t]*(```|~~~)/) {
    indent = RLENGTH - 3
    fence = substr(line, indent + 1, 3)
    info = substr(line, RLENGTH + 1)
    gsub(/^[ \t]+|[ \t]+$/, "", info)
    if (info == "csharp") {
      file = sprintf("%s/SkillExample%02d.cs", dir, ++count)
      printf "" > file
      inside = 1
    } else if (info != "" && info !~ /^(text|bash|powershell|json|yaml|xml|markdown|gitignore|gitattributes|editorconfig)$/) {
      problem("unknown code block tag \"" info "\": use csharp for a complete C# file, inline code for shorter C#, or a known tag")
    }
    next
  }
  fence != "" && line ~ ("^[ \t]*" fence "[ \t]*$") {
    if (inside) close(file)
    fence = ""; inside = 0
    next
  }
  inside { print substr(line, indent + 1) > file }
  END {
    if (fence != "") { printf "%s: a code block is not closed\n", previous > "/dev/stderr"; failed = 1 }
    print count + 0
    exit failed
  }
' "${files[@]}")" || { echo "The code blocks of $skill cannot all be checked: fix the lines above." >&2; exit 1; }

[[ "$count" -gt 0 ]] || { echo "No \`\`\`csharp block found in $skill." >&2; exit 1; }
echo "$count"
