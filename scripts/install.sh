#!/bin/sh
set -eu

REPOSITORY="hatayama/unity-cli-loop"
INSTALL_DIR="${ULOOP_INSTALL_DIR:-$HOME/.local/bin}"
VERSION="${ULOOP_VERSION:-latest}"

detect_asset_name() {
  os=$(uname -s)
  arch=$(uname -m)

  case "$os" in
    Darwin) os_name="darwin" ;;
    *)
      echo "Unsupported OS: $os" >&2
      exit 1
      ;;
  esac

  case "$arch" in
    arm64|aarch64) arch_name="arm64" ;;
    x86_64|amd64) arch_name="amd64" ;;
    *)
      echo "Unsupported architecture: $arch" >&2
      exit 1
      ;;
  esac

  echo "uloop-$os_name-$arch_name.tar.gz"
}

asset_name=$(detect_asset_name)
if [ "$VERSION" = "latest" ]; then
  download_url="https://github.com/$REPOSITORY/releases/latest/download/$asset_name"
else
  download_url="https://github.com/$REPOSITORY/releases/download/$VERSION/$asset_name"
fi

tmp_dir=$(mktemp -d)
trap 'rm -rf "$tmp_dir"' EXIT

mkdir -p "$INSTALL_DIR"
curl -fsSL "$download_url" -o "$tmp_dir/$asset_name"
tar -xzf "$tmp_dir/$asset_name" -C "$tmp_dir"
install -m 0755 "$tmp_dir/uloop" "$INSTALL_DIR/uloop"

case ":$PATH:" in
  *":$INSTALL_DIR:"*) ;;
  *)
    echo "Installed uloop to $INSTALL_DIR, but that directory is not in PATH."
    echo "Add this to your shell profile:"
    echo "  export PATH=\"$INSTALL_DIR:\$PATH\""
    ;;
esac

"$INSTALL_DIR/uloop" --version

