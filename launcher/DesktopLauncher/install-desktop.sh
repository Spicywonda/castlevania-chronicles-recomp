#!/bin/sh
# Register this extracted launcher in the current user's Linux app menu.
set -eu
app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
data_dir=${XDG_DATA_HOME:-"$HOME/.local/share"}
mkdir -p "$data_dir/applications" "$data_dir/icons/hicolor/512x512/apps"
cp "$app_dir/AppIcon.png" "$data_dir/icons/hicolor/512x512/apps/castlevania-chronicles.png"
# Desktop Entry Exec quoting (including percent field-code escaping).
app_exec=$(printf '%s' "$app_dir/Play" | sed 's/\\/\\\\\\\\/g; s/"/\\\\"/g; s/`/\\\\`/g; s/\$/\\\\$/g; s/%/%%/g')
cat > "$data_dir/applications/castlevania-chronicles.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Castlevania Chronicles
Comment=Choose your disc and play
Exec="$app_exec"
Icon=castlevania-chronicles
Terminal=false
Categories=Game;
StartupWMClass=Play
DESKTOP
printf '%s\n' 'Castlevania Chronicles added to your application menu.'
