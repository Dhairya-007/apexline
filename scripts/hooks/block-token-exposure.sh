#!/usr/bin/env bash
# PreToolUse hook on Bash. Denies commands that would dump a full process
# argument vector or the environment.
#
# Why this exists: Unity and Unity Hub pass secrets on the command line -
#   Unity ... -accessToken <token> -hubSessionId <id> -cbmEditorLicense <hash>
# so ANY process-listing command prints live credentials, into the terminal,
# the transcript, and anywhere that text is later pasted. The token itself is
# never in the repo, but it is still a real credential.
#
# Use scripts/unity-status.sh (counts, no args) and scripts/unity-log.sh
# (redacted) instead.
set -uo pipefail

payload=$(cat 2>/dev/null) || exit 0
[ -n "$payload" ] || exit 0

cmd=$(printf '%s' "$payload" | jq -r '.tool_input.command // empty' 2>/dev/null) || exit 0
[ -n "$cmd" ] || exit 0

# Our own safe wrappers are always fine, even though they mention ps/pgrep.
case "$cmd" in
  *scripts/unity-status.sh*|*scripts/unity-log.sh*|*block-token-exposure.sh*) exit 0 ;;
esac

deny () {
  jq -n --arg r "$1" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: $r
    }
  }'
  exit 0
}

# --- Full process argument dumps -------------------------------------------
# ps aux / ps -ef / ps -eo args / ps -o command / pgrep -l -f / pgrep -a
if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]])ps([[:space:]]+-[[:alnum:]]*[aefo][[:alnum:]]*)+'; then
  deny "Blocked: this ps invocation prints full process arguments, and Unity passes -accessToken / -hubSessionId / -cbmEditorLicense there in clear text. Use ./scripts/unity-status.sh (counts only), or pgrep without -f/-l/-a if you only need PIDs."
fi
if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]])ps[[:space:]]+aux'; then
  deny "Blocked: 'ps aux' prints full process arguments, which for Unity includes live access tokens. Use ./scripts/unity-status.sh instead."
fi
if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]])pgrep([[:space:]]+-[[:alnum:]]*[la][[:alnum:]]*)+.*-f|(^|[;&|[:space:]])pgrep[[:space:]]+-[[:alnum:]]*[la]'; then
  deny "Blocked: pgrep -l / -a prints the matched command line including arguments. Use ./scripts/unity-status.sh, or 'pgrep -f <pattern> | wc -l' for a count."
fi

# --- Environment dumps -----------------------------------------------------
if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]])(printenv|env)([[:space:]]*$|[[:space:]]*[;&|])'; then
  deny "Blocked: dumping the whole environment can expose tokens and keys. Read the one variable you need, e.g. \"printenv PATH\"."
fi
if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]])export[[:space:]]+-p|(^|[;&|[:space:]])set[[:space:]]*$'; then
  deny "Blocked: this prints all exported variables, which can include credentials. Read the specific variable instead."
fi

# --- Raw Unity log reads ---------------------------------------------------
# Unity writes its full launch command line, tokens included, near the top of
# Editor.log and the AssetImportWorker logs.
if printf '%s' "$cmd" | grep -qE '(Editor|Editor-prev|upm|AssetImportWorker[0-9A-Za-z]*)\.log'; then
  deny "Blocked: Unity log files contain the launch command line with -accessToken in clear text. Use ./scripts/unity-log.sh, which redacts tokens before printing."
fi

exit 0
