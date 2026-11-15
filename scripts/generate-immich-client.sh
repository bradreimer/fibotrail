#!/usr/bin/env bash
# Writes the C# Immich client in apps/server/src/Fibotrail.Immich/Generated from Immich's OpenAPI
# file. Run it to move to a newer Immich version: change IMMICH_VERSION, run, and commit the result.
#
# The client holds only the routes listed below. They're the reads that Fibotrail is allowed to
# make, per ADR-001 and docs/specs/immich-scanning.md. A test fails when this list and the list in
# Fibotrail.Immich/AllowedRequests.cs differ.

set -euo pipefail

IMMICH_VERSION="v3.4.1"
SPEC="https://raw.githubusercontent.com/immich-app/immich/${IMMICH_VERSION}/open-api/immich-openapi-specs.json"
OUT="src/Fibotrail.Immich/Generated"

cd "$(dirname "$0")/../apps/server"

dotnet tool restore
KIOTA_TUTORIAL_ENABLED=false dotnet kiota generate \
  --language CSharp \
  --openapi "$SPEC" \
  --output "$OUT" \
  --class-name ImmichApiClient \
  --namespace-name Fibotrail.Immich.Generated \
  --include-path "/server/version#GET" \
  --include-path "/search/metadata#POST" \
  --include-path "/assets/statistics#GET" \
  --include-path "/assets/{id}/thumbnail#GET" \
  --exclude-backward-compatible \
  --clean-output \
  --log-level Error

# The log holds nothing the build needs.
rm -f "$OUT/.kiota.log"

# The organization tests want no trailing spaces and one newline at the end of every text file.
find "$OUT" -type f \( -name '*.cs' -o -name '*.json' \) -print0 | while IFS= read -r -d '' file; do
  sed -i -e 's/[[:space:]]*$//' "$file"
  printf '%s\n' "$(cat "$file")" > "$file"
done

echo "Wrote the Immich ${IMMICH_VERSION} client to apps/server/$OUT"
