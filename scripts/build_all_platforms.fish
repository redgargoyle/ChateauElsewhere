#!/usr/bin/env fish

# Build Chateau Chantilly for desktop platforms with Unity 6.
#
# Usage:
#   ./scripts/build_all_platforms.fish all
#   ./scripts/build_all_platforms.fish windows
#   ./scripts/build_all_platforms.fish linux
#   ./scripts/build_all_platforms.fish macos
#   ./scripts/build_all_platforms.fish --dry-run all
#
# Optional environment variables:
#   UNITY_PATH             Absolute path to the Unity executable.
#   CHANTILLY_BUILD_ROOT   Override the default Documents output folder.

function print_usage
    echo "Usage: build_all_platforms.fish [--dry-run] [all|windows|linux|macos]"
    echo
    echo "Builds are written to:"
    echo "  ~/Documents/ChateauChantilly Builds/<Platform>/<timestamp>/"
    echo
    echo "Options:"
    echo "  -n, --dry-run   Validate paths and print Unity commands without building"
    echo "  -h, --help      Show this help"
end

function build_platform --argument-names platform_key display_name unity_target output_name module_name build_argument
    set -l module_path "$chantilly_playback_engines/$module_name"

    if not test -d "$module_path"
        echo "ERROR: $display_name build support is not installed for Unity $chantilly_unity_version."
        echo "Missing module: $module_path"
        return 2
    end

    set -l platform_dir "$chantilly_build_root/$display_name/$chantilly_timestamp"
    set -l output_path "$platform_dir/$output_name"
    set -l log_path "$chantilly_build_root/Logs/$chantilly_timestamp-$platform_key.log"
    set -l unity_args \
        -batchmode \
        -quit \
        -accept-apiupdate \
        -projectPath "$chantilly_project_path" \
        -buildTarget "$unity_target" \
        -standaloneBuildSubtarget Player \
        "$build_argument" "$output_path" \
        -logFile "$log_path"

    echo
    echo "=== $display_name ==="
    echo "Output: $platform_dir"
    echo "Log:    $log_path"

    if test "$chantilly_dry_run" -eq 1
        set -l escaped_command (string escape -- "$chantilly_unity_path" $unity_args)
        echo "Command: "(string join -- ' ' $escaped_command)
        return 0
    end

    mkdir -p "$platform_dir" (path dirname "$log_path")

    command "$chantilly_unity_path" $unity_args
    set -l unity_status $status

    if test $unity_status -ne 0
        echo "ERROR: $display_name build failed with exit code $unity_status."

        if test -f "$log_path"
            echo "Last 60 log lines:"
            tail -n 60 "$log_path"
        end

        return $unity_status
    end

    if not test -e "$output_path"
        echo "ERROR: Unity exited successfully, but the expected build was not created:"
        echo "  $output_path"
        return 1
    end

    echo "Completed: $output_path"
    return 0
end

argparse 'h/help' 'n/dry-run' -- $argv

if test $status -ne 0
    print_usage
    exit 2
end

if set -q _flag_help
    print_usage
    exit 0
end

if test (count $argv) -gt 1
    echo "ERROR: Specify only one build target."
    print_usage
    exit 2
end

set -l requested_target all

if test (count $argv) -eq 1
    set requested_target (string lower -- "$argv[1]")
end

switch "$requested_target"
    case all windows win win64 linux linux64 mac macos osx
    case '*'
        echo "ERROR: Unknown build target '$requested_target'."
        print_usage
        exit 2
end

set -g chantilly_dry_run 0

if set -q _flag_dry_run
    set -g chantilly_dry_run 1
end

set -l script_path (path resolve (status filename))
set -l script_dir (path dirname "$script_path")
set -g chantilly_project_path (path resolve "$script_dir/..")

set -l version_file "$chantilly_project_path/ProjectSettings/ProjectVersion.txt"

if not test -f "$version_file"
    echo "ERROR: Could not find the Unity project version file: $version_file"
    exit 2
end

set -l version_line (string match -r '^m_EditorVersion: .+$' < "$version_file")
set -g chantilly_unity_version (string replace 'm_EditorVersion: ' '' -- "$version_line")

if test -z "$chantilly_unity_version"
    echo "ERROR: Could not read the Unity version from $version_file"
    exit 2
end

set -g chantilly_unity_path ''

if set -q UNITY_PATH
    set chantilly_unity_path "$UNITY_PATH"
else
    set -l unity_candidates \
        "$HOME/Unity/Hub/Editor/$chantilly_unity_version/Editor/Unity" \
        "/opt/Unity/Hub/Editor/$chantilly_unity_version/Editor/Unity" \
        "/Applications/Unity/Hub/Editor/$chantilly_unity_version/Unity.app/Contents/MacOS/Unity"

    for candidate in $unity_candidates
        if test -x "$candidate"
            set chantilly_unity_path "$candidate"
            break
        end
    end
end

if test -z "$chantilly_unity_path"; or not test -x "$chantilly_unity_path"
    echo "ERROR: Unity $chantilly_unity_version was not found."
    echo "Set UNITY_PATH to the matching Unity executable and try again."
    exit 2
end

set -l unity_parent (path dirname "$chantilly_unity_path")

if string match -q '*/Unity.app/Contents/MacOS/Unity' "$chantilly_unity_path"
    set -l unity_contents (path dirname "$unity_parent")
    set -g chantilly_playback_engines "$unity_contents/PlaybackEngines"
else
    set -g chantilly_playback_engines "$unity_parent/Data/PlaybackEngines"
end

if set -q CHANTILLY_BUILD_ROOT
    set -g chantilly_build_root (path resolve "$CHANTILLY_BUILD_ROOT")
else
    set -l documents_dir "$HOME/Documents"

    if type -q xdg-user-dir
        set -l detected_documents (xdg-user-dir DOCUMENTS 2>/dev/null)

        if test -n "$detected_documents"
            set documents_dir "$detected_documents"
        end
    end

    set -g chantilly_build_root "$documents_dir/ChateauChantilly Builds"
end

set -g chantilly_timestamp (date '+%Y-%m-%d_%H-%M-%S')

if test "$chantilly_dry_run" -ne 1; and test -e "$chantilly_project_path/Temp/UnityLockfile"
    echo "ERROR: This Unity project appears to be open in the Editor."
    echo "Close Unity before building, then run this command again."
    echo "Lock file: $chantilly_project_path/Temp/UnityLockfile"
    exit 3
end

echo "Unity:   $chantilly_unity_path"
echo "Version: $chantilly_unity_version"
echo "Project: $chantilly_project_path"
echo "Builds:  $chantilly_build_root"

if test "$chantilly_dry_run" -eq 1
    echo "Mode:    dry run"
end

set -l targets

switch "$requested_target"
    case all
        set targets windows macos linux
    case windows win win64
        set targets windows
    case linux linux64
        set targets linux
    case mac macos osx
        set targets macos
end

set -l failed_targets

for target in $targets
    switch "$target"
        case windows
            build_platform windows Windows win64 ChateauChantilly.exe WindowsStandaloneSupport -buildWindows64Player
        case linux
            build_platform linux Linux linux64 ChateauChantilly.x86_64 LinuxStandaloneSupport -buildLinux64Player
        case macos
            build_platform macos macOS osxuniversal ChateauChantilly.app MacStandaloneSupport -buildOSXUniversalPlayer
    end

    if test $status -ne 0
        set -a failed_targets "$target"
    end
end

echo

if test (count $failed_targets) -gt 0
    echo "Builds finished with failures: "(string join ', ' $failed_targets)
    exit 1
end

if test "$chantilly_dry_run" -eq 1
    echo "Dry run completed successfully."
else
    echo "All requested builds completed successfully."
    echo "Build root: $chantilly_build_root"
end
