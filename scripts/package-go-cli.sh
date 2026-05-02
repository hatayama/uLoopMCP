#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd "$(dirname "$0")/.." && pwd)
GO_CLI_DIR="$ROOT_DIR/Packages/src/GoCli~"
DIST_DIR="$GO_CLI_DIR/dist"
RELEASE_DIR="$GO_CLI_DIR/release"

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

package_unix() {
  platform="$1"
  tmp_dir="$RELEASE_DIR/tmp-$platform"
  mkdir -p "$tmp_dir"
  cp "$DIST_DIR/$platform/uloop-dispatcher" "$tmp_dir/uloop"
  chmod +x "$tmp_dir/uloop"
  (
    cd "$tmp_dir"
    tar -czf "$RELEASE_DIR/uloop-$platform.tar.gz" uloop
  )
  rm -rf "$tmp_dir"
}

package_windows() {
  platform="windows-amd64"
  tmp_dir="$RELEASE_DIR/tmp-$platform"
  mkdir -p "$tmp_dir"
  cp "$DIST_DIR/$platform/uloop-dispatcher.exe" "$tmp_dir/uloop.exe"
  (
    cd "$tmp_dir"
    zip -q "$RELEASE_DIR/uloop-$platform.zip" uloop.exe
  )
  rm -rf "$tmp_dir"
}

package_unix darwin-arm64
package_unix darwin-amd64
package_windows
