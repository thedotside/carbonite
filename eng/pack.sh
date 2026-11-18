#!/usr/bin/env bash
# Packs TheDotSide.Carbonite and TheDotSide.Carbonite.TUnit into artifacts/packages, then checks
# what the archives contain: our assemblies only, the build targets, the README, and an exact
# dependency of the adapter on the core.
#
# Usage: eng/pack.sh [version]          (default: <Version> of Directory.Build.props)
# CARBONITE_FRAMEWORKS="net10.0" limits the frameworks, for a machine without the .NET 11 SDK.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -n 1)}"
frameworks="${CARBONITE_FRAMEWORKS:-net10.0 net11.0}"
output="$repo/artifacts/packages"
# The first Python 3 that really runs: on Windows, "python3" can be a Store shortcut that does not.
python=""
for candidate in python3 python; do
  if "$candidate" -c "import sys, zipfile; sys.exit(sys.version_info < (3, 8))" > /dev/null 2>&1; then
    python="$candidate"
    break
  fi
done
[[ -n "$python" ]] || { echo "eng/pack.sh needs Python 3.8 or later to inspect the packages." >&2; exit 1; }

pack_arguments=(-c Release -o "$output" "-p:Version=$version")
if [[ -n "${CARBONITE_FRAMEWORKS:-}" ]]; then
  pack_arguments+=("-p:TargetFrameworks=${frameworks// /;}")
fi

rm -rf "$output"
mkdir -p "$output"
for project in TheDotSide.Carbonite TheDotSide.Carbonite.TUnit; do
  dotnet pack "$repo/src/$project/$project.csproj" "${pack_arguments[@]}"
done

# Zip inspection through Python: present on every GitHub runner, unlike unzip or a zip-aware tar.
"$python" - "$output" "$version" "$frameworks" <<'PYTHON'
import re
import sys
import zipfile
from pathlib import Path

output, version, frameworks = Path(sys.argv[1]), sys.argv[2], sys.argv[3].split()
problems = []


def check(package_id, required, dependencies):
    archive = output / f"{package_id}.{version}.nupkg"
    if not (output / f"{package_id}.{version}.snupkg").exists():
        problems.append(f"{package_id}: missing symbol package")
    if not archive.exists():
        problems.append(f"{package_id}: missing package {archive.name}")
        return
    with zipfile.ZipFile(archive) as package:
        names = set(package.namelist())
        nuspec = package.read(f"{package_id}.nuspec").decode("utf-8")
    for name in required:
        if name not in names:
            problems.append(f"{package_id}: missing {name}")
    for name in sorted(names):
        # Dependencies stay NuGet references: only this package's own assembly is shipped.
        if name.endswith(".dll") and name.rsplit("/", 1)[-1] != f"{package_id}.dll":
            problems.append(f"{package_id}: unexpected assembly {name}")
    for dependency, expected in dependencies.items():
        pattern = rf'<dependency id="{re.escape(dependency)}" version="([^"]+)"'
        versions = set(re.findall(pattern, nuspec))
        if versions != {expected}:
            problems.append(
                f"{package_id}: dependency {dependency} is {sorted(versions)}, expected {expected}"
            )
    if '<license type="expression">MIT</license>' not in nuspec:
        problems.append(f"{package_id}: MIT license expression missing")
    if "<readme>README.md</readme>" not in nuspec:
        problems.append(f"{package_id}: README missing from the metadata")


def assemblies(name):
    return [f"lib/{tfm}/{name}.{ext}" for tfm in frameworks for ext in ("dll", "xml")]


check(
    "TheDotSide.Carbonite",
    ["README.md", "buildTransitive/TheDotSide.Carbonite.targets", *assemblies("TheDotSide.Carbonite")],
    {},
)
check(
    "TheDotSide.Carbonite.TUnit",
    ["README.md", *assemblies("TheDotSide.Carbonite.TUnit")],
    {"TheDotSide.Carbonite": f"[{version}]", "TUnit.Core": "1.40.0", "TUnit.Assertions": "1.40.0"},
)

if problems:
    print("Package check failed:", *problems, sep="\n  ")
    sys.exit(1)
print(f"Packages {version} checked in {output} ({', '.join(frameworks)})")
PYTHON
