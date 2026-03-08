#!/usr/bin/env sh
set -e
# Root README files are the canonical source.
# This script copies them to Packages/src/ and stages the copies.

# --check mode: verify files are in sync (for CI)
if [ "$1" = "--check" ]; then
  diff -q README.md Packages/src/README.md && diff -q README_ja.md Packages/src/README_ja.md
  exit $?
fi

# Default mode: copy and stage (for pre-commit hook)
cp README.md Packages/src/README.md
cp README_ja.md Packages/src/README_ja.md
git add Packages/src/README.md Packages/src/README_ja.md
