#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd "$(dirname "$0")/.." && pwd)

path_contains_dir() {
  case ":$PATH:" in
    *":$1:"*) return 0 ;;
    *) return 1 ;;
  esac
}

ensure_symlink_target() {
  link_path="$1"

  if [ -e "$link_path" ] && [ ! -L "$link_path" ]; then
    echo "Go CLI source checks passed and dist binaries were rebuilt."
    echo "Refusing to overwrite non-symlink global uloop: $link_path" >&2
    exit 1
  fi
}

update_uloop_link() {
  link_path="$1"

  if [ -L "$link_path" ]; then
    current_target=$(readlink "$link_path")
    echo "Updating global uloop symlink: $link_path -> $current_target"
  else
    echo "Creating global uloop symlink: $link_path"
  fi

  ln -sfn "$dispatcher_path" "$link_path"
  echo "Global uloop now points at the rebuilt dispatcher: $link_path -> $(readlink "$link_path")"
}

"$ROOT_DIR/scripts/check-go-cli-source.sh"
"$ROOT_DIR/scripts/build-go-cli.sh"

dispatcher_path=""
global_command_name="uloop"
existing_uloop_path=""
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
  existing_uloop_path=$(command -v uloop)
  global_bin_dir=$(dirname "$existing_uloop_path")
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
extra_global_uloop_path=""

if [ "$global_command_name" = "uloop.exe" ] && [ -n "$existing_uloop_path" ] && [ "$existing_uloop_path" != "$global_uloop_path" ]; then
  existing_uloop_dir=$(dirname "$existing_uloop_path")
  if [ "$existing_uloop_dir" = "$global_bin_dir" ]; then
    extra_global_uloop_path="$existing_uloop_path"
  fi
fi

echo "Go CLI source checks passed and dist binaries were rebuilt."

ensure_symlink_target "$global_uloop_path"
if [ -n "$extra_global_uloop_path" ]; then
  ensure_symlink_target "$extra_global_uloop_path"
fi

update_uloop_link "$global_uloop_path"
if [ -n "$extra_global_uloop_path" ]; then
  update_uloop_link "$extra_global_uloop_path"
fi

"$global_uloop_path" --version
