#!/usr/bin/env bash
set -euo pipefail
exec "$(dirname "$0")/publish-desktop.sh" win-x64
