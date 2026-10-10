#!/usr/bin/env bash
# JuliaSynth in WSL: a Linux clone kept in step with the Windows repository, built, run or tested there.
#
#   wsl.sh sync [patch]   update the clone to the Windows branch, plus the uncommitted work in the patch file
#   wsl.sh build          sync must come first
#   wsl.sh run [args]     run the app; WSLg shows its window on the Windows desktop
#   wsl.sh test [filter]  run the tests (a dotnet test --filter, optional)
#   wsl.sh publish        a self-contained linux-x64 build in publish/linux-x64 of the clone
#
# WIN_REPO and CLONE override the two folders.
set -euo pipefail

WIN_REPO=${WIN_REPO:-$(cd "$(dirname "$0")/../../.." && pwd)}
CLONE=${CLONE:-$HOME/juliasynth-wsl}

# The SDK in ~/.dotnet comes first: Ubuntu's /usr/bin/dotnet is older than global.json allows.
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$HOME/.dotnet:$HOME/.juliaup/bin:$PATH
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# The SDK major version global.json asks for, installed beside the others if missing.
ensure_sdk() {
    local major
    major=$(sed -n 's/.*"version": *"\([0-9]*\)\..*/\1/p' "$CLONE/global.json")
    if ! { [ -x "$HOME/.dotnet/dotnet" ] && "$HOME/.dotnet/dotnet" --list-sdks | grep -q "^$major\."; }; then
        echo "Installing the .NET $major SDK in ~/.dotnet..."
        curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
        bash /tmp/dotnet-install.sh --channel "$major.0" --install-dir "$HOME/.dotnet"
    fi
}

ensure_fontconfig() {
    if ! dpkg -s libfontconfig1 >/dev/null 2>&1; then
        echo "libfontconfig1 is missing (Skia needs it): sudo apt install libfontconfig1" >&2
        exit 1
    fi
}

sync() {
    local patch=${1:-}
    [ -d "$CLONE/.git" ] || git clone -q "$WIN_REPO" "$CLONE"
    cd "$CLONE"

    # The clone is a build mirror. Changes made in it before it became one are kept in a stash, once;
    # after that, whatever a sync left behind is thrown away.
    if [ ! -e .git/wsl-build-mirror ]; then
        if [ -n "$(git status --porcelain)" ]; then
            git stash push -q -u -m "Changes in the WSL clone before it became a build mirror"
            echo "The clone's own changes are in: $(git stash list | head -1)"
        fi
        touch .git/wsl-build-mirror
    fi
    git reset -q --hard
    git clean -q -fd

    # The Windows branch, as Windows git sees it (WSL git on /mnt/c is slow and sees CRLF everywhere).
    local branch=${BRANCH:?BRANCH is the Windows branch name}
    local commit=${COMMIT:?COMMIT is the Windows HEAD commit}
    git fetch -q origin "$branch"
    git checkout -q -B "$branch" "$commit"

    if [ -n "$patch" ] && [ -s "$patch" ]; then
        git apply --whitespace=nowarn "$patch"
        echo "Applied the uncommitted work: $(git status --short | wc -l) files"
    fi
    echo "Clone at $(git log --oneline -1)"
}

cmd=${1:-build}
shift || true
case "$cmd" in
    sync) sync "$@" ;;
    build) cd "$CLONE"; ensure_sdk; dotnet build JuliaSynth.Desktop -v q -nologo ;;
    run) cd "$CLONE"; ensure_sdk; ensure_fontconfig; dotnet run --project JuliaSynth.Desktop -- "$@" ;;
    test)
        cd "$CLONE"; ensure_sdk
        if [ $# -gt 0 ]; then dotnet test JuliaSynth.Tests --filter "$1"; else dotnet test JuliaSynth.Tests; fi ;;
    publish)
        cd "$CLONE"; ensure_sdk
        dotnet publish JuliaSynth.Desktop -c Release -r linux-x64 --self-contained -o publish/linux-x64 ;;
    *) echo "Unknown command: $cmd (sync, build, run, test or publish)" >&2; exit 2 ;;
esac
