#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
runtime_repo="$project_root/vendor/RecompOne"
for runtime_patch in "$project_root"/patches/runtime/*.patch; do
    [[ -f "$runtime_patch" ]] || continue
    if git -C "$runtime_repo" apply --reverse --check "$runtime_patch" 2>/dev/null; then
        continue
    fi
    git -C "$runtime_repo" apply --check "$runtime_patch"
    git -C "$runtime_repo" apply "$runtime_patch"
done
