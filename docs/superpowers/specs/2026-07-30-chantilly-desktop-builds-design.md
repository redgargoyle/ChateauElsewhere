# Chantilly Desktop Builds Design

## Goal

Produce release builds of the Unity game for Windows, macOS, and Linux under
`~/Desktop/Chantilly Builds`, with platform-specific subfolders and no shipped
artifact or player metadata using the old `dreadforge_2022` or
`dreadforge_2022_2` project names.

## Scope

The shipped product name, company name, standalone application identifier,
executable or app-bundle name, data-folder name, and build output paths will
use `Chantilly`.

Internal developer-only `Dreadforge` identifiers will remain unchanged. This
includes shader lookup names, PlayerPrefs namespaces, editor menu paths,
component menu paths, test strings, and source folder names. Renaming these
identifiers would not improve player-facing naming and would add regression
risk.

## Build Layout

The build root is `~/Desktop/Chantilly Builds`. It contains:

```text
Chantilly Builds/
├── Windows/
│   └── Chantilly.exe
├── macOS/
│   └── Chantilly.app
├── Linux/
│   └── Chantilly.x86_64
└── Logs/
    ├── windows.log
    ├── macos.log
    └── linux.log
```

Unity-generated companion files and directories remain beside their platform
launcher inside the corresponding platform folder. Each run replaces the
prior build for that platform so the requested root remains a simple,
ready-to-distribute set rather than accumulating timestamped copies.

## Architecture

An editor-only C# build entry point owns player metadata, graphics API
selection, enabled-scene discovery, output paths, and build-result validation.
It exposes one batch-mode method that accepts a requested platform and output
root through command-line arguments. This keeps platform-sensitive Unity API
calls inside Unity instead of relying on fragile serialized YAML edits.

The existing Fish shell launcher remains the operator-facing command. It
locates the exact Unity version declared by the project, validates matching
platform support modules, creates the requested folder structure, invokes the
C# entry point once per platform, captures separate logs, and reports all
failures at the end.

## Player Identity

Before building, the Unity entry point sets:

- Company name: `Chantilly`
- Product name: `Chantilly`
- Standalone application identifier: `com.chantilly.chantilly`

These values are also saved in `ProjectSettings/ProjectSettings.asset` so
interactive editor builds and future automation use the same identity.

## Graphics APIs

Automatic graphics API selection is disabled for all three standalone targets.
The explicit lists are:

- Windows x86-64: OpenGL Core only
- Linux x86-64: OpenGL Core only
- macOS universal: Metal only

The build entry point validates the configured API immediately before each
build and fails rather than silently falling back to Direct3D, Vulkan, or
OpenGL on macOS.

## Platform Support

The project uses Unity `6000.4.10f1`. The matching Windows, Linux, and macOS
standalone build-support modules must be installed for that editor. Linux
support is already present; missing Windows and macOS modules will be installed
through Unity Hub before building.

## Failure Handling

The launcher refuses to build while the project is open in another Unity
editor process. Missing editor or platform modules produce a direct diagnostic.
Each Unity invocation writes its own log. A failed platform does not erase a
successful build for another platform, but the overall command exits nonzero
and lists every failed target.

Output for a platform is built into a temporary sibling directory and moved
into its final platform directory only after Unity reports success. This
prevents a failed rebuild from appearing complete.

## Verification

Verification consists of:

1. Edit-mode tests for platform-to-output-name and platform-to-graphics-API
   selection.
2. A dry run of the shell launcher to validate editor and module discovery.
3. Release builds for all three targets.
4. Inspection of every expected launcher and Unity companion directory.
5. Recursive, case-insensitive scans of shipped filenames and readable artifact
   strings for `dreadforge_2022` and `dreadforge_2022_2`.
6. Inspection of Unity logs for successful build completion and the selected
   graphics backend.

The final handoff reports artifact paths, sizes, graphics APIs, naming-scan
results, and any platform-specific limitations such as unsigned macOS app
bundles.
