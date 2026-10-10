---
name: test
description: Run JSynth's tests, all of them or only the areas a change touches (Editing, Arrays, Code, Runs, Streams, Files, Interface), on Windows or in WSL. Use when asked to run or check tests, before a push, or when adding a test and deciding where it goes.
---

# Testing JSynth

Run the tests a change touches before pushing it, and when asked; the user's own preferences on when to test come first. The whole suite takes about 6 minutes on Windows and 3½ in WSL; one area takes a fraction of that.

## The areas

`JSynth.Tests` has one folder per area, and each folder is a namespace, `JSynth.Tests.<Area>`. Helpers everyone uses (`Ui`, `Wait`, `StandInJulia`, `Finished`) stay at the top level.

| Area | What it covers | Run it after changing |
|---|---|---|
| `Editing` | The design through the view model: adding, deleting, wiring and type checks, undo, clipboard, groups, frames, arranging, the library, node colours | `Model/` nodes and wires, `MainViewModel` editing, undo, clipboard, library, layout |
| `Arrays` | Vector and matrix inputs: typed values, array builders, sparse entries, arrays from CSV, `.mtx`, JLD2 and PNG files | array input nodes, value parsing, file loaders |
| `Code` | Function bodies: suggestions, the standard library, `using`/`import` and packages, Julia exported from a design | `JuliaCodeEditor`, completions, packages, script export |
| `Runs` | Running with Julia: the runtime and session, reports and timing, breakpoints, plot data, Display and Save to file, threads | `JSynth.Runtime`, the script generator, output nodes |
| `Streams` | Designs that run as streams: Watch folder, Every, files one at a time, Merge, Zip, Map, live sources | stream nodes, the streaming loop |
| `Files` | Design files: save, open, autosave, the examples and their downloaded data | file format, settings, examples |
| `Interface` | What is drawn and clicked: canvas mouse input, the function panel, node views, parameters panel, plots, the start page, theme, icon, readable text and colours | `Views/`, `Controls/`, `Styles/`, converters |

A change can touch several areas: run each. Run everything when the change is broad (the model's base classes, `MainViewModel` as a whole, `Ui.cs` and the other helpers), or before a push of a large change.

## Running

```powershell
& .claude/skills/test/test.ps1 -Area Interface                # one area
& .claude/skills/test/test.ps1 -Area Streams, Runs            # several
& .claude/skills/test/test.ps1 -Test ZipTests                 # one class
& .claude/skills/test/test.ps1 -Test FrameTests.A_frame_goes_around_the_selected_nodes_and_changes_nothing_worked_out   # one test
& .claude/skills/test/test.ps1                                # everything
& .claude/skills/test/test.ps1 -Area Editing -Wsl             # in WSL (sync the clone first: build skill)
```

The script turns these into a `dotnet test --filter`; by hand that is

```sh
dotnet test JSynth.Tests --filter "FullyQualifiedName~JSynth.Tests.Streams.|FullyQualifiedName~JSynth.Tests.Runs."
```

The trailing dot on an area and the leading dot on a class matter: `~.LoopTests` doesn't also pick `LoopFeatureTests`.

To see which tests an area holds without running them: `dotnet test JSynth.Tests --no-build --list-tests`.

## Reading the results

- Tests run one at a time (`DisableTestParallelization`): the design is shared, static state.
- Many tests in `Runs`, `Streams` and some in other areas need real Julia. Without it they **return early and pass**, checking nothing, so a green run without Julia proves less. Check `julia` is found before trusting them.
- Tests using an example's downloaded data skip themselves when the data isn't there.
- Report failures with the test's name and the assertion message; don't rerun a failure hoping it passes.

## Adding a test

Put it in the area folder whose table row it fits, as a class `<Topic>Tests` in namespace `JSynth.Tests.<Area>`. A new area is a new folder and namespace: add its row above. Don't name an area after one of the app's namespaces (`Views`, `Model`, `Controls`, `Converters`, `Runtime`, `ViewModels`, `Desktop`): code like `Views.MainView` in the tests would then find the test namespace instead.
