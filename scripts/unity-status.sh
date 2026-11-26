#!/usr/bin/env bash
# Safe Unity/Hub inspector. Reports install state and what is running WITHOUT
# printing process arguments, because Unity passes -accessToken,
# -hubSessionId and -cbmEditorLicense on the command line, where any `ps` can
# read them. Use this instead of `ps` when checking on Unity.
set -uo pipefail

HUB_EDITORS=/Applications/Unity/Hub/Editor

echo "=== Installed editors ==="
if [ -d "$HUB_EDITORS" ]; then
  ls -1 "$HUB_EDITORS" 2>/dev/null | while read -r v; do
    [ -z "$v" ] && continue
    printf '  %s' "$v"
    [ -x "$HUB_EDITORS/$v/Unity.app/Contents/MacOS/Unity" ] && printf '  (binary ok)'
    printf '\n'
    for m in "$HUB_EDITORS/$v/PlaybackEngines"/*; do
      [ -d "$m" ] && printf '      module: %s\n' "$(basename "$m")"
    done
  done
else
  echo "  none"
fi

echo
echo "=== Android build support ==="
found=0
for v in "$HUB_EDITORS"/*; do
  [ -d "$v/PlaybackEngines/AndroidPlayer" ] || continue
  found=1
  echo "  $(basename "$v"): present"
  for s in SDK NDK OpenJDK Tools; do
    if [ -d "$v/PlaybackEngines/AndroidPlayer/$s" ]; then
      printf '      %-8s yes\n' "$s"
    else
      printf '      %-8s MISSING\n' "$s"
    fi
  done
done
[ "$found" -eq 0 ] && echo "  NOT INSTALLED - Hub > Installs > gear > Add modules > Android Build Support"

echo
echo "=== Running (counts only; arguments deliberately never printed) ==="
# Counted by piping PIDs to wc -l. The argument vector is never read or
# printed, because Unity passes -accessToken and -hubSessionId there in clear
# text where any process on the machine can read them.
#
# Not using `pgrep -c`: that is a Linux/procps flag. BSD pgrep on macOS exits
# with a usage error (rc=2), which silently reads as "not running".
# Not using `pgrep -x` alone either: the editor runs from a full path.
running () {
  n=$(pgrep -f "$1" 2>/dev/null | wc -l | tr -d ' ')
  printf '  %-26s %s\n' "$2" "$([ "${n:-0}" -gt 0 ] && echo "running ($n)" || echo "not running")"
}
running "Unity.app/Contents/MacOS/Unity"  "Unity Editor"
running "Unity Hub"                       "Unity Hub"
running "Unity.Licensing.Client"          "Licensing client"
running "AssetImportWorker"               "Asset import workers"

echo
echo "=== Project state ==="
root=$(git rev-parse --show-toplevel 2>/dev/null) || root=.
for d in ProjectSettings Packages Library Assets/Settings; do
  printf '  %-20s %s\n' "$d" "$([ -d "$root/$d" ] && echo present || echo missing)"
done
if [ -f "$root/ProjectSettings/ProjectVersion.txt" ]; then
  echo "  version:             $(head -1 "$root/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2-)"
fi

echo
echo "=== Disk ==="
[ -d "$HUB_EDITORS" ] && echo "  editors: $(du -sh "$HUB_EDITORS" 2>/dev/null | cut -f1)"
echo "  free:    $(df -h / | awk 'NR==2 {print $4}')"
