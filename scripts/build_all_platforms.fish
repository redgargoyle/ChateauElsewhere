#!/usr/bin/env fish

# Fish compatibility entry point for the canonical Bash launcher.
set -l script_path (path resolve (status filename))
set -l script_dir (path dirname "$script_path")
set -l bash_launcher "$script_dir/build_all_platforms.sh"

if not test -x "$bash_launcher"
    echo "ERROR: Chantilly Bash build launcher is missing or not executable:" >&2
    echo "  $bash_launcher" >&2
    exit 2
end

exec "$bash_launcher" $argv
