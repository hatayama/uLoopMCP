#!/bin/sh
# Find orphaned .meta files in a Unity project.
# An orphaned .meta is one whose corresponding file or folder no longer exists.
#
# Usage: find-orphaned-meta.sh [directory ...]
#   Defaults to "Assets Packages" if no arguments given.
#
# Output: one orphaned .meta path per line (relative to cwd).
# Exit code: 0 if no orphans, 1 if orphans found.

set -e

if [ $# -eq 0 ]; then
  set -- Assets Packages
fi

tmpfile=$(mktemp)
trap 'rm -f "$tmpfile"' EXIT

for dir in "$@"; do
  [ -d "$dir" ] || continue
  find "$dir" -name "*.meta" -type f | while IFS= read -r meta; do
    target="${meta%.meta}"
    if [ ! -e "$target" ]; then
      echo "$meta"
      echo "1" > "$tmpfile"
    fi
  done
done

if [ -s "$tmpfile" ]; then
  exit 1
fi
exit 0
