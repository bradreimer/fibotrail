#!/usr/bin/env bash
# Runs every check that must pass before a change is done.

set -euo pipefail

cd "$(dirname "$0")/.."

echo "==> Project organization tests"
npm test

if [ -d apps/server ]; then
  (
    cd apps/server
    echo "==> Server: restore"
    dotnet restore
    echo "==> Server: formatting and analyzer rules"
    dotnet format --verify-no-changes --no-restore
    echo "==> Server: build, with warnings as errors"
    dotnet build --no-restore -warnaserror
    echo "==> Server: tests"
    dotnet test --no-build
  )
fi

echo "==> All checks passed"
