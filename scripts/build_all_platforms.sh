#!/usr/bin/env bash

set -uo pipefail

print_usage() {
    cat <<'USAGE'
Usage: build_all_platforms.sh [--dry-run] [all|windows|linux|macos]

Builds are written to:
  ~/Desktop/Chantilly Builds/<Platform>/

Options:
  -n, --dry-run   Validate paths and print Unity commands without building
  -h, --help      Show this help
USAGE
}

chantilly_dry_run=0
chantilly_requested_target="all"
chantilly_target_seen=0

while (($# > 0)); do
    case "$1" in
        -n|--dry-run)
            chantilly_dry_run=1
            ;;
        -h|--help)
            print_usage
            exit 0
            ;;
        all|windows|win|win64|linux|linux64|mac|macos|osx)
            if ((chantilly_target_seen == 1)); then
                echo "ERROR: Specify only one build target." >&2
                print_usage >&2
                exit 2
            fi
            chantilly_requested_target="${1,,}"
            chantilly_target_seen=1
            ;;
        *)
            echo "ERROR: Unknown argument '$1'." >&2
            print_usage >&2
            exit 2
            ;;
    esac
    shift
done

chantilly_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
chantilly_project_path="$(cd "$chantilly_script_dir/.." && pwd -P)"
chantilly_version_file="$chantilly_project_path/ProjectSettings/ProjectVersion.txt"

if [[ ! -f "$chantilly_version_file" ]]; then
    echo "ERROR: Could not find $chantilly_version_file" >&2
    exit 2
fi

chantilly_unity_version="$(
    sed -n 's/^m_EditorVersion: //p' "$chantilly_version_file" | head -n 1
)"

if [[ -z "$chantilly_unity_version" ]]; then
    echo "ERROR: Could not read the Unity version from $chantilly_version_file" >&2
    exit 2
fi

chantilly_user_home="${CHANTILLY_USER_HOME:-$(
    getent passwd "$(id -u)" | cut -d: -f6
)}"

if [[ -z "$chantilly_user_home" ]]; then
    echo "ERROR: Could not determine the current user's home directory." >&2
    exit 2
fi

chantilly_desktop_dir="$chantilly_user_home/Desktop"

if command -v xdg-user-dir >/dev/null 2>&1; then
    chantilly_detected_desktop="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
    if [[ -n "$chantilly_detected_desktop" ]]; then
        chantilly_desktop_dir="$chantilly_detected_desktop"
    fi
fi

chantilly_build_root="${CHANTILLY_BUILD_ROOT:-$chantilly_desktop_dir/Chantilly Builds}"
chantilly_unity_path="${UNITY_PATH:-$chantilly_user_home/Unity/Hub/Editor/$chantilly_unity_version/Editor/Unity}"

if [[ ! -x "$chantilly_unity_path" ]]; then
    echo "ERROR: Unity $chantilly_unity_version was not found at:" >&2
    echo "  $chantilly_unity_path" >&2
    exit 2
fi

chantilly_playback_engines="$(
    cd "$(dirname "$chantilly_unity_path")/Data/PlaybackEngines" 2>/dev/null &&
        pwd -P
)"

if [[ -z "$chantilly_playback_engines" ]]; then
    echo "ERROR: Could not locate Unity playback engines." >&2
    exit 2
fi

if ((chantilly_dry_run == 0)) &&
    [[ -e "$chantilly_project_path/Temp/UnityLockfile" ]]; then
    echo "ERROR: This Unity project is open in another Editor process." >&2
    echo "  $chantilly_project_path/Temp/UnityLockfile" >&2
    exit 3
fi

declare -a chantilly_targets

case "$chantilly_requested_target" in
    all)
        chantilly_targets=(windows macos linux)
        ;;
    windows|win|win64)
        chantilly_targets=(windows)
        ;;
    linux|linux64)
        chantilly_targets=(linux)
        ;;
    mac|macos|osx)
        chantilly_targets=(macos)
        ;;
esac

echo "Unity:   $chantilly_unity_path"
echo "Version: $chantilly_unity_version"
echo "Project: $chantilly_project_path"
echo "Builds:  $chantilly_build_root"

if ((chantilly_dry_run == 1)); then
    echo "Mode:    dry run"
fi

publish_platform() {
    local stage_dir="$1"
    local final_dir="$2"
    local backup_dir="${final_dir}.previous"

    if [[ -e "$backup_dir" ]]; then
        rm -rf -- "$backup_dir"
    fi

    if [[ -e "$final_dir" ]]; then
        mv -- "$final_dir" "$backup_dir"
    fi

    if ! mv -- "$stage_dir" "$final_dir"; then
        if [[ -e "$backup_dir" ]]; then
            mv -- "$backup_dir" "$final_dir"
        fi
        return 1
    fi

    if [[ -e "$backup_dir" ]]; then
        rm -rf -- "$backup_dir"
    fi
}

build_platform() {
    local platform_key="$1"
    local display_name="$2"
    local unity_target="$3"
    local launcher_name="$4"
    local module_name="$5"
    local module_path="$chantilly_playback_engines/$module_name"
    local stage_dir="$chantilly_build_root/.staging/$display_name"
    local stage_launcher="$stage_dir/$launcher_name"
    local final_dir="$chantilly_build_root/$display_name"
    local log_path="$chantilly_build_root/Logs/$platform_key.log"

    if [[ ! -d "$module_path" ]]; then
        echo "ERROR: $display_name build support is not installed." >&2
        echo "  Missing module: $module_path" >&2
        return 2
    fi

    local -a unity_args=(
        -batchmode
        -quit
        -accept-apiupdate
        -projectPath "$chantilly_project_path"
        -buildTarget "$unity_target"
        -standaloneBuildSubtarget Player
        -executeMethod ChantillyDesktopBuild.BuildFromCommandLine
        -chantillyPlatform "$platform_key"
        -chantillyOutputRoot "$chantilly_build_root"
        -logFile "$log_path"
    )

    echo
    echo "=== $display_name ==="
    echo "Output: $final_dir"
    echo "Log:    $log_path"

    if ((chantilly_dry_run == 1)); then
        printf 'Command:'
        printf ' %q' "$chantilly_unity_path" "${unity_args[@]}"
        printf '\n'
        echo "Launcher: $stage_launcher"
        return 0
    fi

    mkdir -p -- "$chantilly_build_root/Logs"

    if ! "$chantilly_unity_path" "${unity_args[@]}"; then
        local unity_status=$?
        echo "ERROR: $display_name build failed with exit code $unity_status." >&2
        if [[ -f "$log_path" ]]; then
            tail -n 80 "$log_path"
        fi
        return "$unity_status"
    fi

    if [[ "$platform_key" == "macos" ]]; then
        if [[ ! -d "$stage_launcher" ]]; then
            echo "ERROR: Missing staged app bundle: $stage_launcher" >&2
            return 1
        fi
    elif [[ ! -f "$stage_launcher" ]]; then
        echo "ERROR: Missing staged launcher: $stage_launcher" >&2
        return 1
    fi

    local legacy_filename
    legacy_filename="$(
        find "$stage_dir" -iname '*dreadforge_2022*' -print -quit
    )"
    if [[ -n "$legacy_filename" ]]; then
        echo "ERROR: Legacy project name found in shipped filename:" >&2
        echo "  $legacy_filename" >&2
        return 1
    fi

    local legacy_string_match
    if legacy_string_match="$(
        LC_ALL=C rg -a -i -l -m 1 'dreadforge_2022(_2)?' "$stage_dir" 2>/dev/null
    )"; then
        echo "ERROR: Legacy project name found in shipped artifact data:" >&2
        echo "$legacy_string_match" >&2
        return 1
    fi

    if ! publish_platform "$stage_dir" "$final_dir"; then
        echo "ERROR: Could not publish $display_name build." >&2
        return 1
    fi

    echo "Completed: $final_dir/$launcher_name"
}

declare -a chantilly_failed_targets=()

for chantilly_target in "${chantilly_targets[@]}"; do
    case "$chantilly_target" in
        windows)
            if ! build_platform \
                windows Windows win64 Chantilly.exe WindowsStandaloneSupport; then
                chantilly_failed_targets+=(windows)
            fi
            ;;
        linux)
            if ! build_platform \
                linux Linux linux64 Chantilly.x86_64 LinuxStandaloneSupport; then
                chantilly_failed_targets+=(linux)
            fi
            ;;
        macos)
            if ! build_platform \
                macos macOS osxuniversal Chantilly.app MacStandaloneSupport; then
                chantilly_failed_targets+=(macos)
            fi
            ;;
    esac
done

echo

if ((${#chantilly_failed_targets[@]} > 0)); then
    printf 'Builds finished with failures: %s\n' "$(
        IFS=', '
        echo "${chantilly_failed_targets[*]}"
    )" >&2
    exit 1
fi

if ((chantilly_dry_run == 1)); then
    echo "Dry run completed successfully."
else
    echo "All requested builds completed successfully."
    echo "Build root: $chantilly_build_root"
fi
