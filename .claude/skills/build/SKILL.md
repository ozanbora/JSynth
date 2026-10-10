---
name: build
description: Build, run, test or publish JuliaSynth on Windows and on Linux under WSL (Ubuntu, shown through WSLg), and check its window with a screenshot. Use when asked to build or launch the app, try a change on Linux or WSL, run the tests on either system, or make a linux-x64 build.
---

# Building JuliaSynth on Windows and WSL

The repository is `C:\Dev\JuliaSynth`. The app is `JuliaSynth.Desktop` (Avalonia 12, .NET 10; `global.json` asks for SDK 10 or newer).

Tests run only when the user says to push, or asks for them (see memory: tests only on push). Builds are fine at any time.

## Windows

From the repository:

```sh
dotnet build JuliaSynth.Desktop -v q -nologo     # 0 Error(s); the 5 warnings are known
dotnet run --project JuliaSynth.Desktop --no-build   # run in the background: it keeps running
dotnet test JuliaSynth.Tests                     # only when asked
```

- Run the app in the background (`run_in_background`); it prints `Running Julia Synth..` and stays open until the window is closed.
- A running copy locks the build output. Close it first: `Stop-Process -Name JuliaSynth.Desktop` (it's the user's window, so only one you started, or with their go-ahead).
- Before starting another copy, check one isn't already open: `Get-Process JuliaSynth.Desktop`.
- Its window title is `<design> - JuliaSynth`.

## WSL (Linux)

Call WSL from the **PowerShell** tool, not Git Bash: Git Bash rewrites `/mnt/c/...` arguments into Windows paths.

The Linux build lives in a clone in the Linux file system, `~/juliasynth-wsl`, never in `/mnt/c` (the two systems would share `bin` and `obj`). Its `origin` is `/mnt/c/Dev/JuliaSynth`. `wsl.sh` in this folder keeps it in step and builds there:

```powershell
$sp = '<scratchpad>'   # any folder Windows and WSL can both reach
git diff HEAD --binary --output="$sp\wip.patch"   # the uncommitted work; --output keeps it byte for byte
$b = git branch --show-current; $c = git rev-parse HEAD
$p = (wsl -e wslpath -a ($sp -replace '\\','/')) + '/wip.patch'
$s = '/mnt/c/Dev/JuliaSynth/.claude/skills/build/wsl.sh'

wsl -e env BRANCH=$b COMMIT=$c bash $s sync $p   # clone = Windows HEAD + uncommitted work
wsl -e bash $s build
wsl -e bash $s run          # in the background, like the Windows run
wsl -e bash $s test         # only when asked; a subset: see the test skill (test.ps1 -Wsl)
wsl -e bash $s publish      # self-contained build in ~/juliasynth-wsl/publish/linux-x64
```

- **Sync before each build.** It throws away anything left in the clone from the last sync; the clone is only a build mirror. The first sync put the clone's own earlier changes in a stash (`git -C ~/juliasynth-wsl stash list`), and that stash is kept.
- Untracked files on Windows aren't in the patch. `git add -N <file>` first if a new file must come along.
- `wsl.sh` puts `~/.dotnet` before Ubuntu's `/usr/bin/dotnet` (8.0, too old), and installs the SDK `global.json` asks for into `~/.dotnet` when it's missing. Julia comes from `~/.juliaup/bin`.
- The window shows on the Windows desktop through WSLg. On Windows it belongs to an `msrdc` process, titled `<design> - JuliaSynth (Ubuntu)`.
- The Linux app keeps its own settings and recent designs in `~/.config/JuliaSynth`, so it may open a different design than Windows does.
- Stop it with `wsl -e pkill -f JuliaSynth.Desktop`.

## Seeing the window

```powershell
& .claude/skills/build/screenshot.ps1 -Out "$sp\shot.png"                    # Windows build
& .claude/skills/build/screenshot.ps1 -Out "$sp\shot.png" -Title '*JuliaSynth (Ubuntu)*'   # WSL build
```

Then Read the picture and look at it: a blank or black frame means it didn't really start.

## When it goes wrong

- **WSL window has a taskbar icon but draws nothing, title starts `[WARN:COPY MODE]`:** WSLg's shared memory failed (`/mnt/wslg/weston.log` says `rdp_allocate_shared_memory`). Not an app bug. `wsl --shutdown` fixes it, but it stops every running distro: ask the user first.
- **`Unable to load shared library 'libSkiaSharp'`:** `sudo apt install libfontconfig1` in Ubuntu (needs the user's password: ask them to run it).
- **`A compatible .NET SDK was not found`:** on Windows, install SDK 10 or newer; in WSL, `wsl.sh build` installs it, so check `~/.dotnet/dotnet --list-sdks`.
- **`git apply` fails during sync:** the patch didn't match Windows HEAD. Make the patch again right before syncing, with `--output` (piping it through PowerShell changes its line endings).
- **Build output locked on Windows:** a JuliaSynth window is still open (see above).
