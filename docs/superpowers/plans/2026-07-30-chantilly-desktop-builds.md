# Chantilly Desktop Builds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce reproducible Windows, macOS, and Linux release builds named Chantilly in organized Desktop subfolders with the required graphics APIs and no legacy project-name leakage.

**Architecture:** An editor-only C# entry point owns Unity player settings, platform mapping, graphics API selection, scene selection, and BuildPipeline validation. Bash and Fish launchers validate the host, create staging/output folders, invoke Unity once per platform, atomically publish successful builds, retain per-platform logs, and scan artifacts for forbidden legacy names.

**Tech Stack:** Unity 6000.4.10f1, C# editor scripting, NUnit edit-mode tests, Bash, Fish, Unity Hub platform modules

## Global Constraints

- Build root: `~/Desktop/Chantilly Builds`
- Company name, product name, standalone identifier namespace, launcher names, and shipped metadata use `Chantilly`.
- Standalone application identifier: `com.chantilly.chantilly`
- Windows x86-64 graphics API: OpenGL Core only.
- Linux x86-64 graphics API: OpenGL Core only.
- macOS universal graphics API: Metal only.
- Internal developer-only `Dreadforge` identifiers remain unchanged.
- Shipped filenames and readable strings must not contain `dreadforge_2022` or `dreadforge_2022_2`, case-insensitively.
- Unity-generated companion files remain beside their platform launcher.
- Platform output is published only after that platform's build succeeds.

---

### Task 1: Platform Specification Tests

**Files:**
- Create: `Assets/Editor/ChantillyDesktopBuildTests.cs`
- Create: `Assets/Editor/ChantillyDesktopBuildTests.cs.meta`
- Test: `Assets/Editor/ChantillyDesktopBuildTests.cs`

**Interfaces:**
- Consumes: `ChantillyDesktopBuild.GetSpecification(string)`
- Produces: regression coverage for launcher names, folders, targets, and graphics APIs

- [ ] **Step 1: Write the failing tests**

Create NUnit cases that assert:

```csharp
[TestCase("windows", "Windows", "Chantilly.exe", BuildTarget.StandaloneWindows64,
    GraphicsDeviceType.OpenGLCore)]
[TestCase("linux", "Linux", "Chantilly.x86_64", BuildTarget.StandaloneLinux64,
    GraphicsDeviceType.OpenGLCore)]
[TestCase("macos", "macOS", "Chantilly.app", BuildTarget.StandaloneOSX,
    GraphicsDeviceType.Metal)]
public void SpecificationUsesExpectedPlatformValues(
    string key,
    string folder,
    string launcher,
    BuildTarget target,
    GraphicsDeviceType graphicsApi)
{
    ChantillyDesktopBuild.Specification spec =
        ChantillyDesktopBuild.GetSpecification(key);
    Assert.That(spec.FolderName, Is.EqualTo(folder));
    Assert.That(spec.LauncherName, Is.EqualTo(launcher));
    Assert.That(spec.Target, Is.EqualTo(target));
    Assert.That(spec.GraphicsApis, Is.EqualTo(new[] { graphicsApi }));
}

[Test]
public void UnknownPlatformIsRejected()
{
    Assert.That(
        () => ChantillyDesktopBuild.GetSpecification("commodore"),
        Throws.ArgumentException);
}
```

- [ ] **Step 2: Run the focused edit-mode test and verify it fails**

Run:

```bash
/home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Unity \
  -batchmode -quit \
  -projectPath /home/hamza/dreadforge_2022_2 \
  -runTests -testPlatform EditMode \
  -testFilter ChantillyDesktopBuildTests \
  -testResults /tmp/chantilly-build-tests.xml \
  -logFile /tmp/chantilly-build-tests.log
```

Expected: nonzero exit or compiler failure because `ChantillyDesktopBuild` does not exist.

- [ ] **Step 3: Commit the failing tests**

```bash
git add Assets/Editor/ChantillyDesktopBuildTests.cs Assets/Editor/ChantillyDesktopBuildTests.cs.meta
git commit -m "test: define Chantilly desktop build matrix"
```

### Task 2: Unity Build Entry Point

**Files:**
- Create: `Assets/Editor/ChantillyDesktopBuild.cs`
- Create: `Assets/Editor/ChantillyDesktopBuild.cs.meta`
- Modify: `ProjectSettings/ProjectSettings.asset`
- Test: `Assets/Editor/ChantillyDesktopBuildTests.cs`

**Interfaces:**
- Consumes: `-chantillyPlatform <windows|linux|macos>` and `-chantillyOutputRoot <absolute path>`
- Produces: `ChantillyDesktopBuild.BuildFromCommandLine()`, `GetSpecification(string)`, and a successful staged Unity player

- [ ] **Step 1: Implement the build matrix and identity configuration**

Create `ChantillyDesktopBuild` with:

```csharp
public const string CompanyName = "Chantilly";
public const string ProductName = "Chantilly";
public const string ApplicationIdentifier = "com.chantilly.chantilly";

internal sealed class Specification
{
    public string Key { get; }
    public string FolderName { get; }
    public string LauncherName { get; }
    public BuildTarget Target { get; }
    public GraphicsDeviceType[] GraphicsApis { get; }
}

internal static Specification GetSpecification(string key)
{
    switch (key?.ToLowerInvariant())
    {
        case "windows":
            return new Specification("windows", "Windows", "Chantilly.exe",
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.OpenGLCore });
        case "linux":
            return new Specification("linux", "Linux", "Chantilly.x86_64",
                BuildTarget.StandaloneLinux64,
                new[] { GraphicsDeviceType.OpenGLCore });
        case "macos":
            return new Specification("macos", "macOS", "Chantilly.app",
                BuildTarget.StandaloneOSX,
                new[] { GraphicsDeviceType.Metal });
        default:
            throw new ArgumentException($"Unsupported Chantilly platform: {key}");
    }
}
```

`ConfigurePlayerSettings` must set:

```csharp
PlayerSettings.companyName = CompanyName;
PlayerSettings.productName = ProductName;
PlayerSettings.SetApplicationIdentifier(
    NamedBuildTarget.Standalone,
    ApplicationIdentifier);
PlayerSettings.SetUseDefaultGraphicsAPIs(spec.Target, false);
PlayerSettings.SetGraphicsAPIs(spec.Target, spec.GraphicsApis);
AssetDatabase.SaveAssets();
```

- [ ] **Step 2: Implement batch-mode argument parsing and building**

`BuildFromCommandLine` must:

1. Read exactly one value after each required argument.
2. Reject a missing, duplicate, or relative output-root argument.
3. Resolve enabled `EditorBuildSettings.scenes` and fail if none exist.
4. Delete and recreate `<output-root>/.staging/<platform-folder>`.
5. build with `BuildOptions.CleanBuildCache` and the mapped target.
6. throw when `BuildReport.summary.result` is not `BuildResult.Succeeded`.
7. confirm the mapped launcher exists.
8. log a single completion line containing target, graphics API, output, size, warnings, and errors.

The build call is:

```csharp
BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
{
    scenes = scenes,
    locationPathName = launcherPath,
    target = spec.Target,
    subtarget = (int)StandaloneBuildSubtarget.Player,
    options = BuildOptions.CleanBuildCache
});
```

- [ ] **Step 3: Run the focused tests and verify they pass**

Run the Task 1 Unity command.

Expected: Unity exits zero and `/tmp/chantilly-build-tests.xml` reports all
`ChantillyDesktopBuildTests` passing.

- [ ] **Step 4: Verify persisted player settings**

Run:

```bash
rg -n "companyName: Chantilly|productName: Chantilly|com\\.chantilly\\.chantilly" \
  ProjectSettings/ProjectSettings.asset
```

Expected: all three values are present and no player identity contains
`dreadforge_2022`.

- [ ] **Step 5: Commit the Unity entry point**

```bash
git add Assets/Editor/ChantillyDesktopBuild.cs \
  Assets/Editor/ChantillyDesktopBuild.cs.meta \
  ProjectSettings/ProjectSettings.asset
git commit -m "feat: add Chantilly desktop build entry point"
```

### Task 3: Host Build Launchers

**Files:**
- Create: `scripts/build_all_platforms.sh`
- Modify: `scripts/build_all_platforms.fish`
- Test: `scripts/build_all_platforms.sh`

**Interfaces:**
- Consumes: optional target `all|windows|linux|macos`, optional `--dry-run`, `UNITY_PATH`, and `CHANTILLY_BUILD_ROOT`
- Produces: published platform folders and `Logs/<platform>.log`

- [ ] **Step 1: Write Bash argument and host validation**

The Bash launcher must use `set -uo pipefail`, default to `all`, derive the
project from the script location, read `ProjectVersion.txt`, and default to:

```bash
chantilly_user_home="${CHANTILLY_USER_HOME:-$(getent passwd "$(id -u)" | cut -d: -f6)}"
chantilly_build_root="${CHANTILLY_BUILD_ROOT:-${XDG_DESKTOP_DIR:-$chantilly_user_home/Desktop}/Chantilly Builds}"
chantilly_unity_path="${UNITY_PATH:-$chantilly_user_home/Unity/Hub/Editor/$chantilly_unity_version/Editor/Unity}"
```

It must reject unknown targets, a missing Unity executable, a project
`Temp/UnityLockfile`, and these missing module directories:

```text
Windows -> WindowsStandaloneSupport
macOS   -> MacStandaloneSupport
Linux   -> LinuxStandaloneSupport
```

- [ ] **Step 2: Implement staged build and atomic publication**

For each requested platform, invoke:

```bash
"$chantilly_unity_path" -batchmode -quit -accept-apiupdate \
  -projectPath "$chantilly_project_path" \
  -buildTarget "$unity_target" \
  -standaloneBuildSubtarget Player \
  -executeMethod ChantillyDesktopBuild.BuildFromCommandLine \
  -chantillyPlatform "$platform_key" \
  -chantillyOutputRoot "$chantilly_build_root" \
  -logFile "$chantilly_build_root/Logs/$platform_key.log"
```

After a successful invocation, replace only the matching final platform folder
with its `.staging/<folder>` directory. Preserve other platform builds and logs.

- [ ] **Step 3: Implement legacy-name verification**

Fail publication if either check finds a match:

```bash
find "$platform_dir" -iname '*dreadforge_2022*' -print -quit
LC_ALL=C rg -a -i -l -m 1 'dreadforge_2022(_2)?' "$platform_dir"
```

The scan intentionally excludes internal plain `Dreadforge` identifiers per
the approved scope.

- [ ] **Step 4: Align the Fish launcher**

Change its default root to `~/Desktop/Chantilly Builds`, remove timestamp
subfolders, change launcher names to `Chantilly.*`, call
`ChantillyDesktopBuild.BuildFromCommandLine`, and use the same staging,
publication, log, and forbidden-name rules as Bash.

- [ ] **Step 5: Validate launcher syntax and dry-run behavior**

Run:

```bash
bash -n scripts/build_all_platforms.sh
bash scripts/build_all_platforms.sh --dry-run linux
```

Expected: Bash syntax passes and the dry run prints a Linux command targeting
`~/Desktop/Chantilly Builds/Linux/Chantilly.x86_64` with no filesystem build.

Because Fish is unavailable on this host, visually compare its target matrix
and command arguments with the passing Bash launcher.

- [ ] **Step 6: Commit the launchers**

```bash
git add scripts/build_all_platforms.sh scripts/build_all_platforms.fish
git commit -m "build: organize Chantilly desktop releases"
```

### Task 4: Install Platform Modules and Produce Builds

**Files:**
- Create externally: `~/Desktop/Chantilly Builds/Windows/**`
- Create externally: `~/Desktop/Chantilly Builds/macOS/**`
- Create externally: `~/Desktop/Chantilly Builds/Linux/**`
- Create externally: `~/Desktop/Chantilly Builds/Logs/**`

**Interfaces:**
- Consumes: Unity Hub installation `6000.4.10f1` and Tasks 2–3
- Produces: three distributable release player folders

- [ ] **Step 1: Install missing matching platform modules**

Use Unity Hub headless module installation for editor `6000.4.10f1` to add
Windows Mono and macOS Mono standalone support:

```bash
/opt/unityhub/unityhub --headless install-modules \
  --version 6000.4.10f1 \
  --module windows-mono mac-mono
```

After installation, verify:

```bash
test -d /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Data/PlaybackEngines/WindowsStandaloneSupport
test -d /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Data/PlaybackEngines/MacStandaloneSupport
test -d /home/hamza/Unity/Hub/Editor/6000.4.10f1/Editor/Data/PlaybackEngines/LinuxStandaloneSupport
```

Expected: all three checks exit zero.

- [ ] **Step 2: Run all release builds**

Run:

```bash
bash scripts/build_all_platforms.sh all
```

Expected: exit zero with completed Windows, macOS, and Linux platform lines.

- [ ] **Step 3: Verify output topology and launchers**

Run:

```bash
test -f "/home/hamza/Desktop/Chantilly Builds/Windows/Chantilly.exe"
test -d "/home/hamza/Desktop/Chantilly Builds/macOS/Chantilly.app"
test -x "/home/hamza/Desktop/Chantilly Builds/Linux/Chantilly.x86_64"
find "/home/hamza/Desktop/Chantilly Builds" -maxdepth 3 -mindepth 1 -printf '%P\n' | sort
du -sh "/home/hamza/Desktop/Chantilly Builds/"{Windows,macOS,Linux}
```

Expected: all launcher checks pass, topology is separated by OS, and each
platform directory has nonzero size.

- [ ] **Step 4: Verify graphics configuration and naming**

Run:

```bash
rg -n "OpenGLCore|Metal|Build completed" "/home/hamza/Desktop/Chantilly Builds/Logs"
find "/home/hamza/Desktop/Chantilly Builds/"{Windows,macOS,Linux} \
  -iname '*dreadforge_2022*' -print
LC_ALL=C rg -a -i -l -m 1 'dreadforge_2022(_2)?' \
  "/home/hamza/Desktop/Chantilly Builds/"{Windows,macOS,Linux}
```

Expected: Windows/Linux logs identify OpenGL Core, macOS identifies Metal, and
both forbidden-name scans produce no output.

- [ ] **Step 5: Run final repository verification**

Run:

```bash
git diff --check
git status --short --branch
```

Expected: no whitespace errors and only intentional build-system changes, if
any, remain.
