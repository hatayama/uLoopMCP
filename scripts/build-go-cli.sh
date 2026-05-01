#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd "$(dirname "$0")/.." && pwd)
GO_CLI_DIR="$ROOT_DIR/Packages/src/GoCli~"
DIST_DIR="$GO_CLI_DIR/dist"

build_binary() {
  os="$1"
  arch="$2"
  name="$3"
  package="$4"
  extension=""

  if [ "$os" = "windows" ]; then
    extension=".exe"
  fi

  output_dir="$DIST_DIR/$os-$arch"
  mkdir -p "$output_dir"

  (
    cd "$GO_CLI_DIR"
    GOOS="$os" GOARCH="$arch" CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o "$output_dir/$name$extension" "$package"
  )
}

build_binary darwin arm64 uloop ./cmd/uloop
build_binary darwin amd64 uloop ./cmd/uloop
build_binary windows amd64 uloop ./cmd/uloop

build_binary darwin arm64 uloop-launcher ./cmd/uloop-launcher
build_binary darwin amd64 uloop-launcher ./cmd/uloop-launcher
build_binary windows amd64 uloop-launcher ./cmd/uloop-launcher

