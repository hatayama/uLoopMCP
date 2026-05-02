#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd "$(dirname "$0")/.." && pwd)
GO_CLI_DIR="$ROOT_DIR/Packages/src/GoCli~"

if ! command -v golangci-lint >/dev/null 2>&1; then
  echo "golangci-lint is required. Install it before running Go CLI checks." >&2
  echo "https://golangci-lint.run/welcome/install/" >&2
  exit 1
fi

unformatted_files=$(
  cd "$GO_CLI_DIR"
  gofmt -l .
)

if [ -n "$unformatted_files" ]; then
  echo "Go files need formatting. Run gofmt -w on these files:" >&2
  printf '%s\n' "$unformatted_files" >&2
  exit 1
fi

(
  cd "$GO_CLI_DIR"
  go vet ./...
  golangci-lint run ./...
  go test ./...
)
