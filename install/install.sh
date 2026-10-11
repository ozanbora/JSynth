#!/bin/sh
# Installs JSynth on Linux (x64) and macOS (Apple Silicon): the latest release, the libraries it needs, a jsynth
# command, a menu entry on Linux, and Julia if it's missing.
#
#   curl -fsSL https://raw.githubusercontent.com/ozanbora/JSynth/master/install/install.sh | sh
#   curl -fsSL https://raw.githubusercontent.com/ozanbora/JSynth/master/install/install.sh | sh -s -- --vnc
#
# Options:
#   --version 1.0.0   a release other than the latest
#   --vnc             also the turbojpeg library, to serve JSynth over VNC (--enableVncServer)
#   --no-julia        don't install Julia
#   --uninstall       remove JSynth; settings, designs and Julia stay
#
# For this user only: system packages (fontconfig, turbojpeg) are the one thing installed with sudo, and only when
# they are missing.
set -eu

REPOSITORY=ozanbora/JSynth
INSTALL_DIR=${JSYNTH_INSTALL_DIR:-$HOME/.local/share/jsynth}
BIN_DIR=${JSYNTH_BIN_DIR:-$HOME/.local/bin}
DESKTOP_FILE=$HOME/.local/share/applications/jsynth.desktop

version=
vnc=
julia=yes
uninstall=
while [ $# -gt 0 ]; do
    case $1 in
        --version) version=${2#v}; shift ;;
        --vnc) vnc=yes ;;
        --no-julia) julia= ;;
        --uninstall) uninstall=yes ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
    shift
done

say() { printf '%s\n' "$*"; }
fail() { printf 'Error: %s\n' "$*" >&2; exit 1; }
has() { command -v "$1" >/dev/null 2>&1; }

if [ -n "$uninstall" ]; then
    rm -rf "$INSTALL_DIR" "$BIN_DIR/jsynth" "$DESKTOP_FILE"
    say "JSynth is uninstalled. Your settings and designs are still in ~/.config/JSynth."
    exit 0
fi

case "$(uname -s)/$(uname -m)" in
    Linux/x86_64) os=linux; rid=linux-x64 ;;
    Darwin/arm64) os=mac; rid=osx-arm64 ;;
    Darwin/x86_64) fail "JSynth is built for Apple Silicon Macs only." ;;
    *) fail "JSynth is built for Linux on x64 and macOS on Apple Silicon, not $(uname -s) on $(uname -m)." ;;
esac

has curl || fail "curl is needed."
has tar || fail "tar is needed."

# --- System libraries -------------------------------------------------------------------------------------------

# Whether the dynamic linker knows a library (Linux).
has_library() { { ldconfig -p 2>/dev/null || /sbin/ldconfig -p 2>/dev/null; } | grep -q "$1"; }

as_root() {
    if [ "$(id -u)" = 0 ]; then "$@"; elif has sudo; then sudo "$@"; else fail "Installing $* needs root: install it, then run this again."; fi
}

# The package for a library under each package manager: apt, dnf, pacman, zypper.
install_package() {
    if has apt-get; then as_root apt-get install -y "$1"
    elif has dnf; then as_root dnf install -y "$2"
    elif has pacman; then as_root pacman -S --needed --noconfirm "$3"
    elif has zypper; then as_root zypper install -y "$4"
    else fail "Install $1 (or your system's package of it) with your package manager, then run this again."
    fi
}

if [ "$os" = linux ]; then
    # Skia, which draws JSynth, needs fontconfig.
    if ! has_library libfontconfig.so.1; then
        say "Installing fontconfig, which JSynth needs to draw text..."
        install_package libfontconfig1 fontconfig fontconfig libfontconfig1
    fi
    if [ -n "$vnc" ] && ! has_library libturbojpeg.so; then
        say "Installing turbojpeg, which VNC needs..."
        install_package libturbojpeg turbojpeg libjpeg-turbo libturbojpeg0
    fi
elif [ -n "$vnc" ] && [ ! -e /opt/homebrew/opt/jpeg-turbo/lib/libturbojpeg.dylib ]; then
    has brew || fail "VNC needs turbojpeg: install Homebrew (https://brew.sh), then run this again, or leave out --vnc."
    say "Installing jpeg-turbo, which VNC needs..."
    brew install jpeg-turbo
fi

# --- JSynth -----------------------------------------------------------------------------------------------------

if [ -n "$version" ]; then
    api=https://api.github.com/repos/$REPOSITORY/releases/tags/v$version
else
    api=https://api.github.com/repos/$REPOSITORY/releases/latest
fi
# The download's address, picked out of the release's JSON without needing jq.
url=$(curl -fsSL "$api" | grep -o "\"browser_download_url\": *\"[^\"]*-$rid\.tar\.gz\"" | sed 's/.*"\(https[^"]*\)"/\1/' | head -n 1)
if [ -z "$url" ]; then
    if [ -n "$version" ]; then fail "There is no JSynth $version for $rid (see https://github.com/$REPOSITORY/releases)."; fi
    fail "The latest JSynth release has no download for $rid."
fi

release=$(basename "$url" | sed "s/^JSynth-\(.*\)-$rid\.tar\.gz$/\1/")
say "Installing JSynth $release in $INSTALL_DIR..."

temp=$(mktemp -d)
trap 'rm -rf "$temp"' EXIT
curl -fsSL "$url" -o "$temp/jsynth.tar.gz"
tar -xzf "$temp/jsynth.tar.gz" -C "$temp"

# The archive holds one JSynth folder; it replaces the one installed before.
rm -rf "$INSTALL_DIR"
mkdir -p "$(dirname "$INSTALL_DIR")" "$BIN_DIR"
mv "$temp/JSynth" "$INSTALL_DIR"
ln -sf "$INSTALL_DIR/JSynth" "$BIN_DIR/jsynth"

if [ "$os" = mac ]; then
    # Downloaded and unsigned: without this, macOS refuses to open it.
    xattr -dr com.apple.quarantine "$INSTALL_DIR" 2>/dev/null || true
else
    mkdir -p "$(dirname "$DESKTOP_FILE")"
    curl -fsSL "https://raw.githubusercontent.com/$REPOSITORY/v$release/JSynth/Assets/Icon/jsynth-256.png" -o "$INSTALL_DIR/jsynth.png" || true
    cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Type=Application
Name=JSynth
Comment=A visual editor for Julia
Exec=$INSTALL_DIR/JSynth
Icon=$INSTALL_DIR/jsynth.png
Terminal=false
Categories=Development;Science;
EOF
    say "Added JSynth to the applications menu."
fi

# --- Julia ------------------------------------------------------------------------------------------------------

# JSynth runs designs with Julia, and finds the one juliaup installs (~/.juliaup/bin).
if has julia || [ -x "$HOME/.juliaup/bin/julia" ]; then
    say "Julia is installed."
elif [ -n "$julia" ]; then
    say "Installing Julia (juliaup)..."
    curl -fsSL https://install.julialang.org | sh -s -- --yes || say "Julia could not be installed: see https://julialang.org/downloads/"
else
    say "Julia is not installed: JSynth needs it to run designs. See https://julialang.org/downloads/"
fi

say ""
case ":$PATH:" in
    *":$BIN_DIR:"*) say "JSynth is installed. Run it with: jsynth" ;;
    *) say "JSynth is installed. Run it with: $BIN_DIR/jsynth (add $BIN_DIR to your PATH to run it as jsynth)" ;;
esac
