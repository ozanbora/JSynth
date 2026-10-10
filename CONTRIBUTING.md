# Contributing to JuliaSynth

Thanks for helping. Bug reports, ideas and pull requests are all welcome.

## Reporting a bug

Open an issue with:

- what you did, what you expected and what happened instead;
- your system (Windows, Linux, WSL), and your Julia version;
- the design file (`.jsynth`), or the example you started from, if the bug is in a design;
- a screenshot, if it's about how something looks.

## Building

You need the [.NET SDK 10](https://dotnet.microsoft.com/download) or newer and, to run designs, [Julia](https://julialang.org/downloads/). The README's [Build and run](README.md#build-and-run) and [Linux and WSL](README.md#linux-and-wsl) sections have the details.

```sh
dotnet run --project JuliaSynth.Desktop
```

## Tests

The tests are in `JuliaSynth.Tests`, one folder per area. Each folder is a namespace, so you can run just the areas your change touches:

| Area | What it covers |
|---|---|
| `Editing` | Changing the design: adding, deleting, wiring, undo, the clipboard, groups, frames, arranging, the library |
| `Arrays` | Vector and matrix inputs, and arrays loaded from files |
| `Code` | Function bodies: suggestions, packages, Julia exported from a design |
| `Runs` | Running with Julia: the session, timing, breakpoints, plot data, output nodes |
| `Streams` | Designs that run as streams: Watch folder, Every, files one at a time, Merge, Zip, Map |
| `Files` | Saving and opening designs, autosave, the examples and their data |
| `Interface` | What is drawn and clicked: the canvas, panels, node views, plots, the start page, the theme |

```sh
dotnet test JuliaSynth.Tests --filter "FullyQualifiedName~JuliaSynth.Tests.Editing."
dotnet test JuliaSynth.Tests --filter "FullyQualifiedName~.GroupTests"
dotnet test JuliaSynth.Tests
```

Things to know:

- **Many tests need Julia, and pass without checking anything when it isn't found.** Install Julia, and the packages the examples use (`CSV`, `JLD2`, `MatrixMarket`, `JSON`, `PNGFiles`), before trusting a green run.
- Tests run one at a time: the design is shared state.
- The whole suite takes about 6 minutes on Windows. CI runs it on Windows and Ubuntu for every pull request.

A new test goes in the folder of its area, as a class `<Topic>Tests` in the namespace `JuliaSynth.Tests.<Area>`. Test names are sentences saying what should happen: `A_group_opened_in_a_frame_makes_room_for_its_nodes_instead_of_lying_on_its_neighbours`.

## Pull requests

- Keep a pull request to one change, with a test that fails without it.
- Match the code around you: its naming, its comments (short, saying why), and its layout.
- Write for people who are new to it, in the app and in the code: plain words, sentence case, no jargon.
- Show Julia's values as Julia prints them; don't reformat them.
- For a change to how something looks, add a before and after screenshot, in the light and dark themes.
- Say in the description what changes for someone using the app.

By contributing, you agree that your contribution is under the project's [MIT License](LICENSE).
