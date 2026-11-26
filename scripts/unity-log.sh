#!/usr/bin/env bash
# Prints a Unity log with credentials redacted.
#
# Unity writes its full launch command line near the top of Editor.log and the
# AssetImportWorker logs, which includes -accessToken, -hubSessionId and
# -cbmEditorLicense. Reading those files raw leaks live credentials into the
# terminal and into anything you paste afterwards.
#
#   ./scripts/unity-log.sh             # tail the editor log, redacted
#   ./scripts/unity-log.sh -n 200      # last 200 lines
#   ./scripts/unity-log.sh --list      # which logs exist
set -uo pipefail

LOGDIR="$HOME/Library/Logs/Unity"
lines=120

case "${1:-}" in
  --list)
    echo "Logs in $LOGDIR:"
    ls -1 "$LOGDIR" 2>/dev/null | sed 's/^/  /' || echo "  none"
    exit 0
    ;;
  -n) lines="${2:-120}" ;;
esac

log="$LOGDIR/Editor.log"
[ -f "$log" ] || { echo "No $log yet (Unity has not run, or logs elsewhere)."; exit 0; }

# Unity writes each launch argument on its OWN LINE in Editor.log, so the
# credential sits on the line AFTER the flag. Inline "-flag value" also occurs
# in batch-mode logs. Both forms are handled: awk blanks the line following a
# sensitive flag, sed covers the same-line case.
SENSITIVE='^[[:space:]]*-(accessToken|hubSessionId|cbmEditorLicense|cbmMachineId|apiKey|token)[[:space:]]*$'

redact () {
  awk -v pat="$SENSITIVE" '
    {
      if (redact_next == 1) { print "<REDACTED>"; redact_next = 0; next }
      if ($0 ~ pat) { redact_next = 1 }
      print
    }
  ' | sed -E \
    -e 's/(-accessToken[ =])[^ ]+/\1<REDACTED>/g' \
    -e 's/(-hubSessionId[ =])[^ ]+/\1<REDACTED>/g' \
    -e 's/(-cbmEditorLicense[ =])[^ ]+/\1<REDACTED>/g' \
    -e 's/(-cbmMachineId[ =])[^ ]+/\1<REDACTED>/g' \
    -e 's/([Tt]oken["'"'"':= ]+)[A-Za-z0-9_.-]{20,}/\1<REDACTED>/g' \
    -e 's/(Bearer )[A-Za-z0-9_.-]{20,}/\1<REDACTED>/g'
}

echo "=== last $lines lines of Editor.log (credentials redacted) ==="
tail -n "$lines" "$log" | redact
