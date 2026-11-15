#!/usr/bin/env bash
# Runs every check that must pass before a change is done.

set -euo pipefail

cd "$(dirname "$0")/.."

echo "==> Project organization tests"
npm test

echo "==> All checks passed"
