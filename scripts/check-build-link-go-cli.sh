#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd "$(dirname "$0")/.." && pwd)

path_contains_dir() {
  case ":$PATH:" in
    *":$1:"*) return 0 ;;
    *) return 1 ;;
  esac
}

"$ROOT_DIR/scripts/check-go-cli-source.sh"
"$ROOT_DIR/scripts/build-go-cli.sh"

dispatcher_path=""
global_command_name="uloop"
os=$(uname -s)
arch=$(uname -m)

case "$os:$arch" in
  Darwin:arm64 | Darwin:aarch64)
    dispatcher_path="$ROOT_DIR/Packages/src/GoCli~/dist/darwin-arm64/uloop-dispatcher"
    ;;
  Darwin:x86_64 | Darwin:amd64)
    dispatcher_path="$ROOT_DIR/Packages/src/GoCli~/dist/darwin-amd64/uloop-dispatcher"
    ;;
  MINGW*:x86_64 | MINGW*:amd64 | MSYS*:x86_64 | MSYS*:amd64 | CYGWIN*:x86_64 | CYGWIN*:amd64 | Windows_NT:x86_64 | Windows_NT:amd64)
    dispatcher_path="$ROOT_DIR/Packages/src/GoCli~/dist/windows-amd64/uloop-dispatcher.exe"
    global_command_name="uloop.exe"
    ;;
esac

if [ -z "$dispatcher_path" ]; then
  echo "Go CLI source checks passed and dist binaries were rebuilt."
  echo "No checked-in dispatcher is mapped for this platform: $os/$arch"
  exit 0
fi

if [ ! -x "$dispatcher_path" ]; then
  echo "Dispatcher was not built or is not executable: $dispatcher_path" >&2
  exit 1
fi

global_bin_dir=""

if [ -n "${ULOOP_GLOBAL_BIN_DIR:-}" ]; then
  global_bin_dir="$ULOOP_GLOBAL_BIN_DIR"
elif command -v uloop >/dev/null 2>&1; then
  uloop_path=$(command -v uloop)
  global_bin_dir=$(dirname "$uloop_path")
elif path_contains_dir "$HOME/.npm-global/bin" || [ -e "$HOME/.npm-global/bin/uloop" ]; then
  global_bin_dir="$HOME/.npm-global/bin"
elif path_contains_dir "$HOME/.local/bin"; then
  global_bin_dir="$HOME/.local/bin"
fi

if [ -z "$global_bin_dir" ]; then
  echo "Go CLI source checks passed and dist binaries were rebuilt."
  echo "No writable PATH directory was selected for global uloop." >&2
  echo "Set ULOOP_GLOBAL_BIN_DIR to the directory that should contain uloop." >&2
  exit 1
fi

mkdir -p "$global_bin_dir"
global_uloop_path="$global_bin_dir/$global_command_name"

if [ -e "$global_uloop_path" ] && [ ! -L "$global_uloop_path" ]; then
  echo "Go CLI source checks passed and dist binaries were rebuilt."
  echo "Refusing to overwrite non-symlink global uloop: $global_uloop_path" >&2
  exit 1
fi

echo "Go CLI source checks passed and dist binaries were rebuilt."
if [ -L "$global_uloop_path" ]; then
  current_target=$(readlink "$global_uloop_path")
  echo "Updating global uloop symlink: $global_uloop_path -> $current_target"
else
  echo "Creating global uloop symlink: $global_uloop_path"
fi

ln -sfn "$dispatcher_path" "$global_uloop_path"
echo "Global uloop now points at the rebuilt dispatcher: $global_uloop_path -> $(readlink "$global_uloop_path")"
"$global_uloop_path" --version
