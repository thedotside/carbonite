#!/usr/bin/env bash
# Uses the packed packages like a real team would, in throwaway projects outside the repository:
# for each target framework, TUnit projects with TheDotSide.Carbonite.TUnit (TUnit at the
# adapter's floor and at the latest version) and one with the core package only (generic mode).
# Each TUnit project goes through the snapshot lifecycle:
#   1. first run on a developer machine creates the snapshots;
#   2. a deterministic build (ContinuousIntegrationBuild=true, paths rewritten to /_/) matches them;
#   3. on CI, a missing snapshot fails;
#   4. a changed value fails with a diff and a received file.
#
# Usage: eng/package-smoke.sh [version]   (run eng/pack.sh first)
# CARBONITE_FRAMEWORKS="net11.0" limits the frameworks, for example without the .NET 10 runtime.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -n 1)}"
frameworks="${CARBONITE_FRAMEWORKS:-net10.0 net11.0}"
# The adapter must work from its floor (the TUnit.Core version it compiles against) to the latest.
tunit_floor="$(sed -n 's:.*<PackageVersion Include="TUnit.Core" Version="\([^"]*\)".*:\1:p' "$repo/Directory.Packages.props")"
tunit_latest="$(sed -n 's:.*<PackageVersion Include="TUnit" Version="\([^"]*\)".*:\1:p' "$repo/Directory.Packages.props")"
tunit_versions="${CARBONITE_TUNIT_VERSIONS:-$tunit_floor $tunit_latest}"
packages="$repo/artifacts/packages"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
# Logs stay in artifacts/ (uploaded by the CI) after the throwaway projects are deleted.
logs="$repo/artifacts/smoke-logs"
rm -rf "$logs"
mkdir -p "$logs"

# A clean package cache: a cached copy of the same version would hide a stale package.
export NUGET_PACKAGES="$work/nuget-cache"
# Paths written into files must be native on Windows (Git Bash passes POSIX paths otherwise).
native() { if command -v cygpath > /dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }
ci_variables=(CI TF_BUILD GITHUB_ACTIONS GITLAB_CI JENKINS_URL TEAMCITY_VERSION BUILDKITE CIRCLECI
  TRAVIS APPVEYOR BITBUCKET_BUILD_NUMBER CODEBUILD_BUILD_ID SNAPSHOT_UPDATE)
developer_machine=()
for variable in "${ci_variables[@]}"; do developer_machine+=(-u "$variable"); done

fail() { printf 'Package smoke test failed: %s\n' "$1" >&2; exit 1; }

# run <project dir> <log name> <pass|fail> [environment assignments...] -- [dotnet test arguments]
run() {
  local project="$1" log="$2" expected="$3"
  shift 3
  # ${array[@]+...} keeps empty arrays safe under set -u in the bash 3.2 of macOS.
  local assignments=()
  while [[ $# -gt 0 && "$1" != "--" ]]; do assignments+=("$1"); shift; done
  [[ $# -gt 0 ]] && shift
  local status=0
  (cd "$project" && env "${developer_machine[@]}" ${assignments[@]+"${assignments[@]}"} dotnet test "$@") > "$logs/$log.log" 2>&1 || status=$?
  if [[ "$expected" == pass && $status -ne 0 ]]; then
    tail -n 40 "$logs/$log.log"
    fail "$log: expected the tests to pass"
  fi
  if [[ "$expected" == fail && $status -eq 0 ]]; then
    tail -n 40 "$logs/$log.log"
    fail "$log: expected the tests to fail"
  fi
}

expect_output() { grep -q -- "$2" "$logs/$1.log" || { tail -n 40 "$logs/$1.log"; fail "$1: '$2' not found"; }; }
expect_file() { [[ -f "$1" ]] || fail "missing file ${1#"$work"/}"; }
# Without /_/ paths in the test assembly, a "deterministic" step proves nothing.
expect_mapped_paths() {
  grep -a -q "/_/" "$1/bin/Debug/$2/Smoke.Tests.dll" ||
    fail "${1#"$work"/}: the deterministic build kept the real source paths"
}

create_project() {
  local directory="$1" framework="$2" package="$3" tunit="$4"
  mkdir -p "$directory"
  cat > "$directory/nuget.config" << EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$(native "$packages")" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="TheDotSide.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
  printf '{ "test": { "runner": "Microsoft.Testing.Platform" } }\n' > "$directory/global.json"
  cat > "$directory/Smoke.Tests.csproj" << EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" Version="$tunit" />
    <PackageReference Include="$package" Version="$version" />
  </ItemGroup>
</Project>
EOF
  # A Git repository, so that ContinuousIntegrationBuild really rewrites the paths to /_/.
  git -C "$directory" init -q
  printf 'bin/\nobj/\n*.received.*\n' > "$directory/.gitignore"
}

for framework in $frameworks; do
for tunit in $tunit_versions; do
  echo "== $framework, TheDotSide.Carbonite.TUnit with TUnit $tunit"
  project="$work/$framework/tunit-$tunit"
  name="$framework-tunit-$tunit"
  create_project "$project" "$framework" TheDotSide.Carbonite.TUnit "$tunit"
  cat > "$project/SmokeTests.cs" << 'EOF'
using TheDotSide.Carbonite;
using TheDotSide.Carbonite.TUnit;

namespace Smoke;

public class SmokeTests
{
    [Test]
    public void Object() => Snapshot.Match(new { Name = "Ada", Total = 1.5m, Id = Guid.NewGuid() });

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public void Case(int number) => Snapshot.Match(new { number });

    [Test]
    public async Task Assertion() => await Assert.That(new[] { 1, 2 }).MatchesSnapshot();
}
EOF
  git -C "$project" add -A && git -C "$project" -c user.name=smoke -c user.email=smoke@example.invalid commit -q -m smoke
  snapshots="$project/__snapshots__"

  run "$project" "$name-create" pass
  for snapshot in Object Case_1 Case_2 Assertion; do
    expect_file "$snapshots/SmokeTests.$snapshot.verified.json"
  done
  grep -q '"Id": "Guid_1"' "$snapshots/SmokeTests.Object.verified.json" || fail "the Guid was not scrubbed"

  # A fresh build in CI conditions: an incremental build would keep the previous source paths.
  rm -rf "$project/bin" "$project/obj"
  run "$project" "$name-deterministic" pass CI=true -- -p:ContinuousIntegrationBuild=true
  expect_output "$name-deterministic" "réussie : 4\|succeeded: 4\|Passed: 4\|passed: 4"
  expect_mapped_paths "$project" "$framework"

  rm "$snapshots/SmokeTests.Case_2.verified.json"
  run "$project" "$name-ci" fail CI=true
  expect_output "$name-ci" "Snapshot missing: SmokeTests.Case_2"
  expect_file "$snapshots/SmokeTests.Case_2.received.json"

  run "$project" "$name-recreate" pass
  perl -pi -e 's/Total = 1\.5m/Total = 2.5m/' "$project/SmokeTests.cs"
  run "$project" "$name-mismatch" fail
  expect_output "$name-mismatch" 'Snapshot mismatch: SmokeTests.Object'
  expect_output "$name-mismatch" '+  "Total": 2.5,'
  expect_file "$snapshots/SmokeTests.Object.received.json"
done

  echo "== $framework, TheDotSide.Carbonite (generic mode)"
  project="$work/$framework/core"
  create_project "$project" "$framework" TheDotSide.Carbonite "$tunit_latest"
  cat > "$project/GenericTests.cs" << 'EOF'
using TheDotSide.Carbonite;

namespace Smoke;

public class GenericTests
{
    [Test]
    public void Object() => Snapshot.Match(new { Mode = "generic" });
}
EOF
  git -C "$project" add -A && git -C "$project" -c user.name=smoke -c user.email=smoke@example.invalid commit -q -m smoke
  run "$project" "$framework-core-create" pass
  expect_file "$project/__snapshots__/GenericTests.Object.verified.json"
  rm -rf "$project/bin" "$project/obj"
  run "$project" "$framework-core-deterministic" pass CI=true -- -p:ContinuousIntegrationBuild=true
  expect_mapped_paths "$project" "$framework"
done

echo "Package smoke test passed for $version ($frameworks; TUnit $tunit_versions)."
